"""JWT-protected team management APIs (create, invite stub, list).

All routes require a Bearer access token. Responses expose team/org ids and
workstream labels — never invite emails in list payloads beyond the invite
endpoint's own confirmation (and even there we avoid echoing into logs).
"""

from __future__ import annotations

from typing import Literal

from fastapi import APIRouter, HTTPException, status
from pydantic import BaseModel, EmailStr, Field

from app.api.deps import CurrentAuth, SessionDep
from app.core.logging import get_logger
from app.models.team import TeamRole
from app.services import teams as team_service

router = APIRouter(prefix="/teams", tags=["teams"])
logger = get_logger(__name__)


class OrganizationCreate(BaseModel):
    """Optional inline org create when posting a team."""

    name: str = Field(min_length=1, max_length=255)
    slug: str = Field(min_length=1, max_length=64)


class TeamCreateRequest(BaseModel):
    """
    Create a team under an existing org, or create org+team in one call.

    Provide either `organization_id` or `organization` (name+slug).
    """

    organization_id: str | None = Field(default=None, max_length=36)
    organization: OrganizationCreate | None = None
    name: str = Field(min_length=1, max_length=255)
    workstream: Literal["api", "ui", "infra", "mobile", "ai", "general"] = "general"
    description: str | None = Field(default=None, max_length=512)


class TeamResponse(BaseModel):
    id: str
    organization_id: str
    name: str
    workstream: str
    description: str | None = None


class InviteMemberRequest(BaseModel):
    """Email invite stub — records pending membership; does not send mail yet."""

    email: EmailStr
    role: Literal["admin", "developer", "viewer"] = "developer"


class InviteMemberResponse(BaseModel):
    membership_id: str
    team_id: str
    role: str
    status: str = "pending"
    # Intentionally omit invite email from response body in prep to reduce
    # accidental logging of PII in API gateways; clients already know the email.


class TeamListResponse(BaseModel):
    teams: list[TeamResponse]


@router.post(
    "",
    response_model=TeamResponse,
    status_code=status.HTTP_201_CREATED,
    summary="Create a team (JWT)",
)
def create_team(body: TeamCreateRequest, auth: CurrentAuth, session: SessionDep) -> TeamResponse:
    """Create team + owner membership for the authenticated subject."""
    try:
        if body.organization_id:
            organization_id = body.organization_id
        elif body.organization is not None:
            org = team_service.ensure_organization(
                session,
                name=body.organization.name,
                slug=body.organization.slug,
                created_by_user_id=auth.subject,
            )
            organization_id = org.id
        else:
            raise HTTPException(
                status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
                detail="Provide organization_id or organization",
            )

        team = team_service.create_team(
            session,
            organization_id=organization_id,
            name=body.name,
            workstream=body.workstream,
            created_by_user_id=auth.subject,
            description=body.description,
        )
    except team_service.TeamServiceError as exc:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(exc)) from exc

    return TeamResponse(
        id=team.id,
        organization_id=team.organization_id,
        name=team.name,
        workstream=team.workstream,
        description=team.description,
    )


@router.get(
    "",
    response_model=TeamListResponse,
    summary="List teams for the current user (JWT)",
)
def list_my_teams(
    auth: CurrentAuth,
    session: SessionDep,
    organization_id: str | None = None,
) -> TeamListResponse:
    """List teams where the caller has an accepted membership."""
    teams = team_service.list_teams_for_user(
        session,
        user_id=auth.subject,
        organization_id=organization_id,
    )
    return TeamListResponse(
        teams=[
            TeamResponse(
                id=t.id,
                organization_id=t.organization_id,
                name=t.name,
                workstream=t.workstream,
                description=t.description,
            )
            for t in teams
        ]
    )


@router.post(
    "/{team_id}/invites",
    response_model=InviteMemberResponse,
    status_code=status.HTTP_201_CREATED,
    summary="Invite a member by email (stub, JWT)",
)
def invite_member(
    team_id: str,
    body: InviteMemberRequest,
    auth: CurrentAuth,
    session: SessionDep,
) -> InviteMemberResponse:
    """
    Stub invite: persists pending TeamMembership; optional Slack notify.

    Email delivery (SES) is intentionally not wired here — assessment-day can
    attach SES without changing the membership model.
    """
    role = TeamRole(body.role)
    try:
        membership = team_service.invite_member(
            session,
            team_id=team_id,
            invite_email=str(body.email),
            role=role,
            invited_by_user_id=auth.subject,
        )
    except team_service.TeamServiceError as exc:
        # Map not-found vs conflict lightly for the stub.
        detail = str(exc)
        code = (
            status.HTTP_404_NOT_FOUND
            if "not found" in detail
            else status.HTTP_400_BAD_REQUEST
        )
        raise HTTPException(status_code=code, detail=detail) from exc

    return InviteMemberResponse(
        membership_id=membership.id,
        team_id=membership.team_id,
        role=membership.role.value,
        status="pending",
    )
