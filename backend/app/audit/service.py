"""
Audit write + query + export helpers.

Writes are append-only. Exports stream CSV/JSON for admin review without embedding
PHI (actors remain opaque ids; metadata is pre-sanitized).
"""

from __future__ import annotations

import csv
import io
import json
import re
from datetime import datetime
from typing import Any, Iterable

from sqlmodel import Session, col, select

from app.alerts.templates import assert_no_phi
from app.audit.models import AuditEvent
from app.core.logging import get_logger, log_event

logger = get_logger(__name__)

_ACTION_RE = re.compile(r"^[a-z][a-z0-9_]{0,63}$")
_RESOURCE_RE = re.compile(r"^[a-z][a-z0-9_]{0,63}$")


class AuditError(ValueError):
    """Raised when an audit write would violate immutability or PHI rules."""


def _validate_metadata(metadata: dict[str, Any] | None) -> str | None:
    """Serialize metadata after rejecting PHI-looking values."""
    if not metadata:
        return None
    safe: dict[str, Any] = {}
    for key, value in metadata.items():
        if not re.fullmatch(r"[a-z][a-z0-9_]{0,63}", key):
            raise AuditError("metadata keys must be short snake_case tokens")
        if isinstance(value, str):
            assert_no_phi(value, field=f"metadata.{key}")
            if len(value) > 128:
                raise AuditError("metadata string values must be <= 128 chars")
            safe[key] = value
        elif isinstance(value, bool):
            safe[key] = value
        elif isinstance(value, int):
            # bool/int only — no floats/lists that could smuggle content.
            safe[key] = value
        else:
            raise AuditError("metadata values must be str|bool|int")
    return json.dumps(safe, separators=(",", ":"), sort_keys=True)


def record_event(
    session: Session,
    *,
    action: str,
    resource_type: str,
    actor_user_id: str | None = None,
    resource_id: str | None = None,
    request_id: str | None = None,
    metadata: dict[str, Any] | None = None,
    commit: bool = True,
) -> AuditEvent:
    """Append an immutable audit row. Never pass emails or free-text PHI."""
    if not _ACTION_RE.fullmatch(action):
        raise AuditError("action must be a short snake_case token")
    if not _RESOURCE_RE.fullmatch(resource_type):
        raise AuditError("resource_type must be a short snake_case token")
    if resource_id is not None and len(resource_id) > 64:
        raise AuditError("resource_id too long")

    event = AuditEvent(
        actor_user_id=actor_user_id,
        action=action,
        resource_type=resource_type,
        resource_id=resource_id,
        request_id=request_id,
        metadata_json=_validate_metadata(metadata),
    )
    session.add(event)
    if commit:
        session.commit()
        session.refresh(event)
    else:
        session.flush()
    log_event(logger, "audit_recorded", action=action, resource_type=resource_type)
    return event


def list_events(
    session: Session,
    *,
    limit: int = 100,
    offset: int = 0,
    action: str | None = None,
    actor_user_id: str | None = None,
) -> list[AuditEvent]:
    """Newest-first page of audit events for admin review."""
    limit = max(1, min(limit, 1000))
    offset = max(0, offset)
    statement = select(AuditEvent).order_by(col(AuditEvent.created_at).desc())
    if action:
        statement = statement.where(AuditEvent.action == action)
    if actor_user_id:
        statement = statement.where(AuditEvent.actor_user_id == actor_user_id)
    statement = statement.offset(offset).limit(limit)
    return list(session.exec(statement).all())


def event_to_dict(event: AuditEvent) -> dict[str, Any]:
    """Stable export shape — ids and codes only."""
    meta: dict[str, Any] | None = None
    if event.metadata_json:
        try:
            parsed = json.loads(event.metadata_json)
            if isinstance(parsed, dict):
                meta = parsed
        except json.JSONDecodeError:
            meta = None
    created = event.created_at
    created_iso = None
    if isinstance(created, datetime):
        created_iso = created.isoformat()
    return {
        "id": event.id,
        "actor_user_id": event.actor_user_id,
        "action": event.action,
        "resource_type": event.resource_type,
        "resource_id": event.resource_id,
        "request_id": event.request_id,
        "metadata": meta,
        "created_at": created_iso,
    }


def export_json(events: Iterable[AuditEvent]) -> str:
    return json.dumps([event_to_dict(e) for e in events], indent=2)


def export_csv(events: Iterable[AuditEvent]) -> str:
    buffer = io.StringIO()
    writer = csv.DictWriter(
        buffer,
        fieldnames=[
            "id",
            "actor_user_id",
            "action",
            "resource_type",
            "resource_id",
            "request_id",
            "metadata",
            "created_at",
        ],
    )
    writer.writeheader()
    for event in events:
        row = event_to_dict(event)
        row["metadata"] = json.dumps(row["metadata"] or {}, separators=(",", ":"))
        writer.writerow(row)
    return buffer.getvalue()
