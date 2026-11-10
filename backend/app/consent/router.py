"""Consent HTTP API — users manage their own AI/media consent flags."""

from __future__ import annotations

from datetime import datetime

from fastapi import APIRouter, HTTPException, status
from pydantic import BaseModel, Field

from app.api.deps import CurrentAuth, RequireAdmin, RequireUser, SessionDep
from app.consent import service as consent_service
from app.consent.models import KNOWN_PURPOSES, ConsentRecord

router = APIRouter(prefix="/consent", tags=["consent"])


class ConsentUpdateRequest(BaseModel):
    purpose: str = Field(min_length=1, max_length=64, pattern=r"^[a-z][a-z0-9_]{0,63}$")
    granted: bool


class ConsentOut(BaseModel):
    id: str
    subject_user_id: str
    purpose: str
    granted: bool
    granted_at: datetime | None = None
    revoked_at: datetime | None = None


class ConsentPurposesOut(BaseModel):
    purposes: list[str]


def _to_out(record: ConsentRecord) -> ConsentOut:
    return ConsentOut(
        id=record.id,
        subject_user_id=record.subject_user_id,
        purpose=record.purpose,
        granted=record.granted,
        granted_at=record.granted_at,
        revoked_at=record.revoked_at,
    )


@router.get("/purposes", response_model=ConsentPurposesOut)
def list_known_purposes(_auth: RequireUser) -> ConsentPurposesOut:
    return ConsentPurposesOut(purposes=sorted(KNOWN_PURPOSES))


@router.get("/me", response_model=list[ConsentOut])
def list_my_consents(auth: CurrentAuth, session: SessionDep) -> list[ConsentOut]:
    rows = consent_service.list_consents(session, subject_user_id=auth.subject)
    return [_to_out(r) for r in rows]


@router.put("/me", response_model=ConsentOut)
def update_my_consent(
    auth: CurrentAuth,
    session: SessionDep,
    body: ConsentUpdateRequest,
) -> ConsentOut:
    try:
        record = consent_service.set_consent(
            session,
            subject_user_id=auth.subject,
            purpose=body.purpose,
            granted=body.granted,
            actor_user_id=auth.subject,
        )
    except consent_service.ConsentError as exc:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(exc)) from exc
    return _to_out(record)


@router.get("/subjects/{subject_user_id}", response_model=list[ConsentOut])
def admin_list_subject_consents(
    _auth: RequireAdmin,
    session: SessionDep,
    subject_user_id: str,
) -> list[ConsentOut]:
    rows = consent_service.list_consents(session, subject_user_id=subject_user_id)
    return [_to_out(r) for r in rows]
