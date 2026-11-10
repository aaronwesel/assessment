"""
Webhook outbox enqueue + retry delivery with HMAC signatures.

Delivery uses httpx when `webhooks` feature flag is on. Tests can inject a
transport stub via `deliver_one(..., http_post=...)`.
"""

from __future__ import annotations

import json
import re
from datetime import UTC, datetime, timedelta
from typing import Any, Callable
from urllib.parse import urlparse

import httpx
from sqlmodel import Session, col, select

from app.alerts.templates import assert_no_phi
from app.audit.service import record_event
from app.core.config import get_settings
from app.core.logging import get_logger, log_event
from app.flags.service import is_feature_enabled
from app.webhooks.hmac import sign_body
from app.webhooks.models import WebhookOutbox

logger = get_logger(__name__)

_EVENT_RE = re.compile(r"^[a-z][a-z0-9_]{0,63}$")
HttpPost = Callable[[str, bytes, dict[str, str]], tuple[int, str]]


class WebhookError(ValueError):
    pass


def _utcnow() -> datetime:
    return datetime.now(UTC)


def _validate_payload(payload: dict[str, Any]) -> str:
    """Serialize payload after PHI/structure checks."""
    for key, value in payload.items():
        if not re.fullmatch(r"[a-z][a-z0-9_]{0,63}", key):
            raise WebhookError("payload keys must be short snake_case tokens")
        if isinstance(value, str):
            assert_no_phi(value, field=f"payload.{key}")
            if len(value) > 256:
                raise WebhookError("payload string values must be <= 256 chars")
        elif isinstance(value, bool):
            continue
        elif isinstance(value, int):
            continue
        elif value is None:
            continue
        else:
            raise WebhookError("payload values must be str|bool|int|null")
    return json.dumps(payload, separators=(",", ":"), sort_keys=True)


def enqueue(
    session: Session,
    *,
    event_type: str,
    destination_url: str,
    payload: dict[str, Any],
    actor_user_id: str | None = None,
    commit: bool = True,
) -> WebhookOutbox:
    """Insert a pending outbox row (call inside the same transaction as business writes)."""
    if not _EVENT_RE.fullmatch(event_type):
        raise WebhookError("event_type must be a short snake_case token")
    parsed = urlparse(destination_url)
    if parsed.scheme not in {"https", "http"} or not parsed.netloc:
        raise WebhookError("destination_url must be an absolute http(s) URL")
    # Prefer https outside local/test — still allow http for pytest stubs.
    settings = get_settings()
    if settings.environment not in {"local", "test"} and parsed.scheme != "https":
        raise WebhookError("destination_url must use https outside local/test")

    row = WebhookOutbox(
        event_type=event_type,
        destination_url=destination_url,
        payload_json=_validate_payload(payload),
        status="pending",
        attempts=0,
        max_attempts=settings.webhook_max_attempts,
        next_attempt_at=_utcnow(),
    )
    session.add(row)
    if commit:
        session.commit()
        session.refresh(row)
    else:
        session.flush()

    record_event(
        session,
        action="webhook_enqueued",
        resource_type="webhook_outbox",
        actor_user_id=actor_user_id,
        resource_id=row.id,
        metadata={"event_type": event_type},
        commit=commit,
    )
    log_event(logger, "webhook_enqueued", event_type=event_type)
    return row


def _backoff_seconds(attempts: int) -> int:
    # 2^attempts seconds, capped (attempt 1 → 2s, …).
    return min(3600, 2 ** max(1, attempts))


def _default_http_post(url: str, body: bytes, headers: dict[str, str]) -> tuple[int, str]:
    response = httpx.post(url, content=body, headers=headers, timeout=10.0)
    return response.status_code, response.reason_phrase or ""


def deliver_one(
    session: Session,
    row: WebhookOutbox,
    *,
    http_post: HttpPost | None = None,
) -> WebhookOutbox:
    """Attempt one delivery. Updates status / next_attempt_at / dead-letter."""
    if not is_feature_enabled("webhooks"):
        row.last_error_code = "feature_disabled"
        session.add(row)
        session.commit()
        session.refresh(row)
        return row

    settings = get_settings()
    secret = settings.webhook_signing_secret or settings.secret_key
    body = row.payload_json.encode("utf-8")
    signature = sign_body(body, secret=secret)
    headers = {
        "Content-Type": "application/json",
        "X-Assessment-Signature": signature,
        "X-Assessment-Event": row.event_type,
        "X-Assessment-Delivery": row.id,
    }

    post = http_post or _default_http_post
    row.attempts += 1
    try:
        status_code, _reason = post(row.destination_url, body, headers)
        row.last_status_code = status_code
        if 200 <= status_code < 300:
            row.status = "delivered"
            row.delivered_at = _utcnow()
            row.last_error_code = None
            row.next_attempt_at = None
            log_event(logger, "webhook_delivered", event_type=row.event_type)
        else:
            row.last_error_code = f"http_{status_code}"
            if row.attempts >= row.max_attempts:
                row.status = "dead"
                row.next_attempt_at = None
            else:
                row.status = "pending"
                row.next_attempt_at = _utcnow() + timedelta(
                    seconds=_backoff_seconds(row.attempts)
                )
            log_event(
                logger,
                "webhook_delivery_failed",
                event_type=row.event_type,
                status_code=status_code,
            )
    except httpx.HTTPError:
        row.last_error_code = "transport_error"
        if row.attempts >= row.max_attempts:
            row.status = "dead"
            row.next_attempt_at = None
        else:
            row.status = "pending"
            row.next_attempt_at = _utcnow() + timedelta(seconds=_backoff_seconds(row.attempts))
        log_event(logger, "webhook_transport_error", event_type=row.event_type)

    session.add(row)
    session.commit()
    session.refresh(row)
    return row


def process_due(
    session: Session,
    *,
    limit: int = 20,
    http_post: HttpPost | None = None,
) -> list[WebhookOutbox]:
    """Deliver up to `limit` due pending outbox rows."""
    now = _utcnow()
    # Fetch pending rows then filter due-ness in Python for SQLite/Postgres parity.
    statement = (
        select(WebhookOutbox)
        .where(WebhookOutbox.status == "pending")
        .order_by(col(WebhookOutbox.created_at).asc())
        .limit(max(1, min(limit, 100)) * 3)
    )
    candidates = list(session.exec(statement).all())
    due: list[WebhookOutbox] = []
    for row in candidates:
        nxt = row.next_attempt_at
        if nxt is None:
            due.append(row)
        else:
            if nxt.tzinfo is None:
                nxt = nxt.replace(tzinfo=UTC)
            if nxt <= now:
                due.append(row)
        if len(due) >= limit:
            break
    return [deliver_one(session, row, http_post=http_post) for row in due]


def list_outbox(
    session: Session,
    *,
    status: str | None = None,
    limit: int = 100,
) -> list[WebhookOutbox]:
    limit = max(1, min(limit, 500))
    statement = select(WebhookOutbox).order_by(col(WebhookOutbox.created_at).desc()).limit(limit)
    if status:
        statement = statement.where(WebhookOutbox.status == status)
    return list(session.exec(statement).all())
