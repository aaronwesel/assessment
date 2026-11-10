"""Break-glass HTTP API — admin-only emergency access grants."""

from __future__ import annotations

from datetime import datetime

from fastapi import APIRouter, HTTPException, Query, status
from pydantic import BaseModel, Field

from app.api.deps import RequireAdmin, SessionDep
from app.break_glass import service as bg_service
from app.break_glass.models import BreakGlassGrant

router = APIRouter(prefix="/break-glass", tags=["break-glass"])


class BreakGlassCreateRequest(BaseModel):
    target_user_id: str = Field(min_length=1, max_length=36)
    reason_code: str = Field(min_length=1, max_length=64, pattern=r"^[a-z][a-z0-9_]{0,63}$")
    ticket_ref: str | None = Field(default=None, max_length=64)
    reason_detail: str | None = Field(default=None, max_length=500)
    ttl_minutes: int | None = Field(default=None, ge=5, le=24 * 60)


class BreakGlassOut(BaseModel):
    id: str
    granted_by_user_id: str
    target_user_id: str
    reason_code: str
    ticket_ref: str | None = None
    expires_at: datetime
    revoked_at: datetime | None = None
    created_at: datetime | None = None
    active: bool


def _to_out(grant: BreakGlassGrant) -> BreakGlassOut:
    return BreakGlassOut(
        id=grant.id,
        granted_by_user_id=grant.granted_by_user_id,
        target_user_id=grant.target_user_id,
        reason_code=grant.reason_code,
        ticket_ref=grant.ticket_ref,
        expires_at=grant.expires_at,
        revoked_at=grant.revoked_at,
        created_at=grant.created_at,
        active=bg_service.is_grant_active(grant),
    )


@router.post("/grants", response_model=BreakGlassOut, status_code=201)
def create_break_glass_grant(
    auth: RequireAdmin,
    session: SessionDep,
    body: BreakGlassCreateRequest,
) -> BreakGlassOut:
    try:
        grant = bg_service.create_grant(
            session,
            granted_by_user_id=auth.subject,
            target_user_id=body.target_user_id,
            reason_code=body.reason_code,
            ticket_ref=body.ticket_ref,
            reason_detail=body.reason_detail,
            ttl_minutes=body.ttl_minutes,
        )
    except (bg_service.BreakGlassError, ValueError) as exc:
        # ValueError covers AlertTemplateError from PHI guards on reason_detail.
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(exc)) from exc
    return _to_out(grant)


@router.get("/grants", response_model=list[BreakGlassOut])
def list_break_glass_grants(
    _auth: RequireAdmin,
    session: SessionDep,
    active_only: bool = Query(default=False),
    limit: int = Query(default=100, ge=1, le=500),
) -> list[BreakGlassOut]:
    grants = bg_service.list_grants(session, active_only=active_only, limit=limit)
    return [_to_out(g) for g in grants]


@router.post("/grants/{grant_id}/revoke", response_model=BreakGlassOut)
def revoke_break_glass_grant(
    auth: RequireAdmin,
    session: SessionDep,
    grant_id: str,
) -> BreakGlassOut:
    try:
        grant = bg_service.revoke_grant(
            session, grant_id=grant_id, actor_user_id=auth.subject
        )
    except bg_service.BreakGlassError as exc:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=str(exc)) from exc
    return _to_out(grant)
