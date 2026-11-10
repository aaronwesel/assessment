"""Consent grant/revoke persistence and lookups."""

from __future__ import annotations

import re
from datetime import UTC, datetime

from sqlmodel import Session, select

from app.audit.service import record_event
from app.consent.models import KNOWN_PURPOSES, ConsentRecord
from app.core.logging import get_logger, log_event

logger = get_logger(__name__)

_PURPOSE_RE = re.compile(r"^[a-z][a-z0-9_]{0,63}$")


class ConsentError(ValueError):
    pass


def _utcnow() -> datetime:
    return datetime.now(UTC)


def get_consent(session: Session, *, subject_user_id: str, purpose: str) -> ConsentRecord | None:
    statement = select(ConsentRecord).where(
        ConsentRecord.subject_user_id == subject_user_id,
        ConsentRecord.purpose == purpose,
    )
    return session.exec(statement).first()


def is_consent_granted(session: Session, *, subject_user_id: str, purpose: str) -> bool:
    record = get_consent(session, subject_user_id=subject_user_id, purpose=purpose)
    return bool(record and record.granted and record.revoked_at is None)


def set_consent(
    session: Session,
    *,
    subject_user_id: str,
    purpose: str,
    granted: bool,
    actor_user_id: str | None = None,
) -> ConsentRecord:
    """Upsert a consent flag for a purpose. Audited; no PHI in purpose strings."""
    if not _PURPOSE_RE.fullmatch(purpose):
        raise ConsentError("purpose must be a short snake_case token")
    if purpose not in KNOWN_PURPOSES:
        raise ConsentError(f"unknown consent purpose: {purpose}")

    record = get_consent(session, subject_user_id=subject_user_id, purpose=purpose)
    now = _utcnow()
    if record is None:
        record = ConsentRecord(
            subject_user_id=subject_user_id,
            purpose=purpose,
            granted=granted,
            granted_at=now if granted else None,
            revoked_at=None if granted else now,
            updated_by_user_id=actor_user_id,
        )
    else:
        record.granted = granted
        record.updated_by_user_id = actor_user_id
        if granted:
            record.granted_at = now
            record.revoked_at = None
        else:
            record.revoked_at = now
    session.add(record)
    session.commit()
    session.refresh(record)

    record_event(
        session,
        action="consent_granted" if granted else "consent_revoked",
        resource_type="consent_record",
        actor_user_id=actor_user_id,
        resource_id=record.id,
        metadata={"purpose": purpose, "granted": granted},
    )
    log_event(logger, "consent_updated", purpose=purpose, granted=granted)
    return record


def list_consents(
    session: Session,
    *,
    subject_user_id: str,
) -> list[ConsentRecord]:
    statement = select(ConsentRecord).where(ConsentRecord.subject_user_id == subject_user_id)
    return list(session.exec(statement).all())
