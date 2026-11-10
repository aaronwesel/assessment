"""
Break-glass grant lifecycle: create (time-boxed), list active, revoke.

Mandatory `reason_code` + optional short detail (PHI-guarded). Each grant is
audited and triggers SES/Slack alert hooks.
"""

from __future__ import annotations

import re
from datetime import UTC, datetime, timedelta

from sqlmodel import Session, col, select

from app.alerts.templates import assert_no_phi
from app.audit.service import record_event
from app.break_glass.alerts import notify_break_glass_granted
from app.break_glass.models import BreakGlassGrant
from app.core.config import get_settings
from app.core.logging import get_logger, log_event

logger = get_logger(__name__)

_REASON_RE = re.compile(r"^[a-z][a-z0-9_]{0,63}$")


class BreakGlassError(ValueError):
    pass


def _utcnow() -> datetime:
    return datetime.now(UTC)


def create_grant(
    session: Session,
    *,
    granted_by_user_id: str,
    target_user_id: str,
    reason_code: str,
    ticket_ref: str | None = None,
    reason_detail: str | None = None,
    ttl_minutes: int | None = None,
) -> BreakGlassGrant:
    """Create a time-boxed emergency-access grant with mandatory reason."""
    if not _REASON_RE.fullmatch(reason_code):
        raise BreakGlassError("reason_code must be a short snake_case token")
    if not target_user_id.strip():
        raise BreakGlassError("target_user_id is required")
    if ticket_ref:
        assert_no_phi(ticket_ref, field="ticket_ref")
        if len(ticket_ref) > 64:
            raise BreakGlassError("ticket_ref too long")
    detail = None
    if reason_detail:
        cleaned = reason_detail.strip()
        assert_no_phi(cleaned, field="reason_detail")
        if len(cleaned) > 500:
            raise BreakGlassError("reason_detail too long")
        detail = cleaned

    settings = get_settings()
    minutes = ttl_minutes if ttl_minutes is not None else settings.break_glass_default_ttl_minutes
    minutes = max(5, min(int(minutes), settings.break_glass_max_ttl_minutes))
    expires_at = _utcnow() + timedelta(minutes=minutes)

    grant = BreakGlassGrant(
        granted_by_user_id=granted_by_user_id,
        target_user_id=target_user_id,
        reason_code=reason_code,
        ticket_ref=ticket_ref,
        reason_detail=detail,
        expires_at=expires_at,
    )
    session.add(grant)
    session.commit()
    session.refresh(grant)

    record_event(
        session,
        action="break_glass_grant",
        resource_type="break_glass_grant",
        actor_user_id=granted_by_user_id,
        resource_id=grant.id,
        metadata={"reason_code": reason_code, "ttl_minutes": minutes},
    )
    notify_break_glass_granted(grant_id=grant.id, reason_code=reason_code)
    log_event(logger, "break_glass_granted", reason_code=reason_code, ttl_minutes=minutes)
    return grant


def is_grant_active(grant: BreakGlassGrant, *, now: datetime | None = None) -> bool:
    moment = now or _utcnow()
    if grant.revoked_at is not None:
        return False
    expires = grant.expires_at
    if expires.tzinfo is None:
        expires = expires.replace(tzinfo=UTC)
    return expires > moment


def list_grants(
    session: Session,
    *,
    active_only: bool = False,
    limit: int = 100,
) -> list[BreakGlassGrant]:
    limit = max(1, min(limit, 500))
    statement = select(BreakGlassGrant).order_by(col(BreakGlassGrant.created_at).desc()).limit(limit)
    rows = list(session.exec(statement).all())
    if active_only:
        now = _utcnow()
        return [g for g in rows if is_grant_active(g, now=now)]
    return rows


def revoke_grant(
    session: Session,
    *,
    grant_id: str,
    actor_user_id: str,
) -> BreakGlassGrant:
    grant = session.get(BreakGlassGrant, grant_id)
    if grant is None:
        raise BreakGlassError("grant not found")
    if grant.revoked_at is not None:
        return grant
    grant.revoked_at = _utcnow()
    session.add(grant)
    session.commit()
    session.refresh(grant)
    record_event(
        session,
        action="break_glass_revoke",
        resource_type="break_glass_grant",
        actor_user_id=actor_user_id,
        resource_id=grant.id,
        metadata={"reason_code": grant.reason_code},
    )
    log_event(logger, "break_glass_revoked")
    return grant


def has_active_grant(session: Session, *, target_user_id: str) -> bool:
    """True if any non-revoked, non-expired grant exists for the target user."""
    statement = select(BreakGlassGrant).where(BreakGlassGrant.target_user_id == target_user_id)
    now = _utcnow()
    for grant in session.exec(statement).all():
        if is_grant_active(grant, now=now):
            return True
    return False
