"""
Admin audit-log HTTP surface: list + CSV/JSON export (immutable reads).

Writes happen from other services via `record_event` — there is intentionally
no public POST that accepts free-text bodies (reduces PHI injection risk).
"""

from __future__ import annotations

from typing import Any, Literal

from fastapi import APIRouter, HTTPException, Query, Response, status
from pydantic import BaseModel, Field

from app.api.deps import RequireAdmin, SessionDep
from app.audit import service as audit_service

router = APIRouter(prefix="/audit", tags=["audit"])


class AuditEventOut(BaseModel):
    id: str
    actor_user_id: str | None = None
    action: str
    resource_type: str
    resource_id: str | None = None
    request_id: str | None = None
    metadata: dict[str, Any] | None = None
    created_at: str | None = None


class AuditListResponse(BaseModel):
    items: list[AuditEventOut]
    limit: int
    offset: int


@router.get("/events", response_model=AuditListResponse)
def list_audit_events(
    _auth: RequireAdmin,
    session: SessionDep,
    limit: int = Query(default=100, ge=1, le=1000),
    offset: int = Query(default=0, ge=0),
    action: str | None = Query(default=None, max_length=64),
    actor_user_id: str | None = Query(default=None, max_length=36),
) -> AuditListResponse:
    """Admin-only newest-first audit page — opaque ids only."""
    events = audit_service.list_events(
        session,
        limit=limit,
        offset=offset,
        action=action,
        actor_user_id=actor_user_id,
    )
    return AuditListResponse(
        items=[AuditEventOut(**audit_service.event_to_dict(e)) for e in events],
        limit=limit,
        offset=offset,
    )


@router.get("/export")
def export_audit_events(
    _auth: RequireAdmin,
    session: SessionDep,
    format: Literal["json", "csv"] = Query(default="json"),
    limit: int = Query(default=1000, ge=1, le=5000),
    action: str | None = Query(default=None, max_length=64),
) -> Response:
    """
    Export audit rows as JSON or CSV for offline review.

    HIPAA: payload contains opaque actor/resource ids and action codes only —
    never emails, medical content, or free-text reasons with PHI.
    """
    events = audit_service.list_events(session, limit=limit, offset=0, action=action)
    if format == "csv":
        body = audit_service.export_csv(events)
        return Response(
            content=body,
            media_type="text/csv",
            headers={"Content-Disposition": 'attachment; filename="audit-export.csv"'},
        )
    body = audit_service.export_json(events)
    return Response(
        content=body,
        media_type="application/json",
        headers={"Content-Disposition": 'attachment; filename="audit-export.json"'},
    )


class ManualAuditNote(BaseModel):
    """Admin-recorded structured note — codes only, no free-text PHI."""

    action: str = Field(max_length=64, pattern=r"^[a-z][a-z0-9_]{0,63}$")
    resource_type: str = Field(max_length=64, pattern=r"^[a-z][a-z0-9_]{0,63}$")
    resource_id: str | None = Field(default=None, max_length=64)
    metadata: dict[str, Any] | None = None


@router.post("/events", response_model=AuditEventOut, status_code=201)
def create_structured_audit_event(
    auth: RequireAdmin,
    session: SessionDep,
    body: ManualAuditNote,
) -> AuditEventOut:
    """Allow admins to append a structured (non-PHI) audit note."""
    try:
        event = audit_service.record_event(
            session,
            action=body.action,
            resource_type=body.resource_type,
            actor_user_id=auth.subject,
            resource_id=body.resource_id,
            metadata=body.metadata,
        )
    except (audit_service.AuditError, ValueError) as exc:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=str(exc),
        ) from exc
    return AuditEventOut(**audit_service.event_to_dict(event))
