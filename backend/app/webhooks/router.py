"""Webhook outbox admin API — enqueue, list, process retries."""

from __future__ import annotations

from datetime import datetime
from typing import Any

from fastapi import APIRouter, HTTPException, Query, status
from pydantic import BaseModel, Field, HttpUrl

from app.api.deps import RequireAdmin, SessionDep
from app.core.config import get_settings
from app.webhooks import service as webhook_service
from app.webhooks.models import WebhookOutbox

router = APIRouter(prefix="/webhooks", tags=["webhooks"])


class EnqueueRequest(BaseModel):
    event_type: str = Field(min_length=1, max_length=64, pattern=r"^[a-z][a-z0-9_]{0,63}$")
    destination_url: HttpUrl
    payload: dict[str, Any] = Field(default_factory=dict)


class OutboxOut(BaseModel):
    id: str
    event_type: str
    destination_host: str
    status: str
    attempts: int
    max_attempts: int
    last_status_code: int | None = None
    last_error_code: str | None = None
    next_attempt_at: datetime | None = None
    delivered_at: datetime | None = None
    created_at: datetime | None = None


def _host_only(url: str) -> str:
    from urllib.parse import urlparse

    return urlparse(url).netloc or "unknown"


def _to_out(row: WebhookOutbox) -> OutboxOut:
    # Never echo full destination URL (may contain secrets in query string).
    return OutboxOut(
        id=row.id,
        event_type=row.event_type,
        destination_host=_host_only(row.destination_url),
        status=row.status,
        attempts=row.attempts,
        max_attempts=row.max_attempts,
        last_status_code=row.last_status_code,
        last_error_code=row.last_error_code,
        next_attempt_at=row.next_attempt_at,
        delivered_at=row.delivered_at,
        created_at=row.created_at,
    )


@router.post("/outbox", response_model=OutboxOut, status_code=201)
def enqueue_webhook(
    auth: RequireAdmin,
    session: SessionDep,
    body: EnqueueRequest,
) -> OutboxOut:
    try:
        row = webhook_service.enqueue(
            session,
            event_type=body.event_type,
            destination_url=str(body.destination_url),
            payload=body.payload,
            actor_user_id=auth.subject,
        )
    except webhook_service.WebhookError as exc:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(exc)) from exc
    return _to_out(row)


@router.get("/outbox", response_model=list[OutboxOut])
def list_webhook_outbox(
    _auth: RequireAdmin,
    session: SessionDep,
    status_filter: str | None = Query(default=None, alias="status", max_length=32),
    limit: int = Query(default=100, ge=1, le=500),
) -> list[OutboxOut]:
    rows = webhook_service.list_outbox(session, status=status_filter, limit=limit)
    return [_to_out(r) for r in rows]


@router.post("/outbox/process", response_model=list[OutboxOut])
def process_webhook_outbox(
    _auth: RequireAdmin,
    session: SessionDep,
    limit: int = Query(default=20, ge=1, le=100),
) -> list[OutboxOut]:
    """
    Process due outbox rows (stub worker entrypoint).

    In production this would run from a scheduled worker/ECS task; exposing an
    admin trigger keeps local Compose and pytest simple.
    """
    settings = get_settings()
    if not settings.webhooks_enabled:
        # Still allow process when Redis flag overrides; service checks flag.
        pass
    rows = webhook_service.process_due(session, limit=limit)
    return [_to_out(r) for r in rows]
