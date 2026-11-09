"""Team management service stubs — create, invite, list.

Persistence is SQLModel; invite "email" is a stub that records a pending
membership row. Optional Slack notify is fire-and-forget and never includes
the invite email in the webhook body (HIPAA: no PHI/PII in Slack).
"""

from __future__ import annotations

import re
from typing import Sequence

from sqlmodel import Session, select

from app.clients.slack import SlackNotifier, get_slack_client
from app.core.logging import get_logger, log_event
from app.models.organization import Organization
from app.models.team import Team, TeamMembership, TeamRole

logger = get_logger(__name__)

# Slug: lowercase alnum + hyphens; keeps org URLs non-PHI and stable.
_SLUG_RE = re.compile(r"^[a-z0-9][a-z0-9-]{1,62}[a-z0-9]$|^[a-z0-9]$")


class TeamServiceError(ValueError):
    """Domain validation / conflict errors mapped to HTTP by the route layer."""


def _normalize_slug(slug: str) -> str:
    normalized = slug.strip().lower()
    if not _SLUG_RE.match(normalized):
        raise TeamServiceError(
            "slug must be 1–64 chars of lowercase letters, digits, and hyphens"
        )
    return normalized


def ensure_organization(
    session: Session,
    *,
    name: str,
    slug: str,
    created_by_user_id: str,
) -> Organization:
    """Get-or-create org by slug so create-team can be called in one shot."""
    normalized = _normalize_slug(slug)
    existing = session.exec(
        select(Organization).where(Organization.slug == normalized)
    ).first()
    if existing is not None:
        return existing
    org = Organization(
        name=name.strip() or normalized,
        slug=normalized,
        created_by_user_id=created_by_user_id,
    )
    session.add(org)
    session.commit()
    session.refresh(org)
    # Audit without PHI: ids and slug only.
    log_event(
        logger,
        "organization_created",
        organization_id=org.id,
        slug=org.slug,
        actor_subject=created_by_user_id,
    )
    return org


def create_team(
    session: Session,
    *,
    organization_id: str,
    name: str,
    workstream: str,
    created_by_user_id: str,
    description: str | None = None,
    slack: SlackNotifier | None = None,
) -> Team:
    """Create a team and seed the creator as owner membership."""
    org = session.get(Organization, organization_id)
    if org is None:
        raise TeamServiceError("organization not found")

    clean_name = name.strip()
    if not clean_name:
        raise TeamServiceError("team name is required")

    clean_stream = (workstream or "general").strip().lower()
    if not re.fullmatch(r"[a-z][a-z0-9_]{0,63}", clean_stream):
        raise TeamServiceError("workstream must be a short snake_case token")

    team = Team(
        organization_id=organization_id,
        name=clean_name,
        workstream=clean_stream,
        description=(description.strip() if description else None),
        created_by_user_id=created_by_user_id,
    )
    session.add(team)
    session.flush()

    # Creator becomes owner so the team always has a steward.
    membership = TeamMembership(
        team_id=team.id,
        user_id=created_by_user_id,
        role=TeamRole.OWNER,
        invited_by_user_id=created_by_user_id,
        accepted_at=team.created_at,  # may be None until refresh; OK for stub
    )
    session.add(membership)
    session.commit()
    session.refresh(team)

    log_event(
        logger,
        "team_created",
        team_id=team.id,
        organization_id=organization_id,
        workstream=clean_stream,
        actor_subject=created_by_user_id,
    )

    # Optional Slack: team id + workstream only — never names/emails.
    notifier = slack if slack is not None else get_slack_client()
    notifier.notify_team_invite(
        event="team_created",
        team_id=team.id,
        workstream=clean_stream,
        role=TeamRole.OWNER.value,
    )
    return team


def invite_member(
    session: Session,
    *,
    team_id: str,
    invite_email: str,
    role: TeamRole,
    invited_by_user_id: str,
    slack: SlackNotifier | None = None,
) -> TeamMembership:
    """
    Record a pending invite by email (stub — no SES send here).

    Email is stored for later acceptance wiring; Slack/ops notifications must
    never echo the address (use team_id + role tokens only).
    """
    team = session.get(Team, team_id)
    if team is None:
        raise TeamServiceError("team not found")

    email = invite_email.strip().lower()
    if "@" not in email or len(email) > 320:
        raise TeamServiceError("invite_email looks invalid")

    if role == TeamRole.OWNER:
        raise TeamServiceError("cannot invite a second owner via invite stub")

    # Conflict check: same email already pending/member on this team.
    existing = session.exec(
        select(TeamMembership).where(
            TeamMembership.team_id == team_id,
            TeamMembership.invite_email == email,
        )
    ).first()
    if existing is not None:
        raise TeamServiceError("invite already exists for this team")

    membership = TeamMembership(
        team_id=team_id,
        user_id=None,
        invite_email=email,
        role=role,
        invited_by_user_id=invited_by_user_id,
        accepted_at=None,
    )
    session.add(membership)
    session.commit()
    session.refresh(membership)

    # HIPAA: do not put invite_email in structured logs.
    log_event(
        logger,
        "team_invite_created",
        team_id=team_id,
        membership_id=membership.id,
        role=role.value,
        actor_subject=invited_by_user_id,
    )

    notifier = slack if slack is not None else get_slack_client()
    notifier.notify_team_invite(
        event="team_invite",
        team_id=team_id,
        workstream=team.workstream,
        role=role.value,
    )
    return membership


def list_teams_for_user(
    session: Session,
    *,
    user_id: str,
    organization_id: str | None = None,
) -> Sequence[Team]:
    """List teams where the user has an accepted membership (user_id set)."""
    statement = (
        select(Team)
        .join(TeamMembership, TeamMembership.team_id == Team.id)
        .where(TeamMembership.user_id == user_id)
    )
    if organization_id:
        statement = statement.where(Team.organization_id == organization_id)
    return list(session.exec(statement).unique().all())


def list_teams_for_organization(
    session: Session,
    *,
    organization_id: str,
) -> Sequence[Team]:
    """List all teams under an organization (JWT-gated at the route)."""
    return list(
        session.exec(
            select(Team).where(Team.organization_id == organization_id)
        ).all()
    )
