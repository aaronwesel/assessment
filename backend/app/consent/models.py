"""
Consent flags for AI and other sensitive processing purposes.

Each row is a (subject_user_id, purpose) grant/revoke timeline. AI clients
(DeepSeek, Rekognition) must call `consent.gate` before sending data off-box.
Purposes are short codes — never free-text clinical descriptions.
"""

from __future__ import annotations

from datetime import datetime
from uuid import uuid4

from sqlalchemy import Column, DateTime, UniqueConstraint, func
from sqlmodel import Field, SQLModel


class ConsentRecord(SQLModel, table=True):
    __tablename__ = "consent_records"
    __table_args__ = (
        UniqueConstraint("subject_user_id", "purpose", name="uq_consent_subject_purpose"),
    )

    id: str = Field(default_factory=lambda: str(uuid4()), primary_key=True)
    subject_user_id: str = Field(max_length=36, index=True)
    # e.g. ai_deepseek, ai_rekognition, media_processing
    purpose: str = Field(max_length=64, index=True)
    granted: bool = Field(default=False)
    granted_at: datetime | None = Field(
        default=None,
        sa_column=Column(DateTime(timezone=True), nullable=True),
    )
    revoked_at: datetime | None = Field(
        default=None,
        sa_column=Column(DateTime(timezone=True), nullable=True),
    )
    updated_by_user_id: str | None = Field(default=None, max_length=36)
    created_at: datetime | None = Field(
        default=None,
        sa_column=Column(DateTime(timezone=True), server_default=func.now(), nullable=False),
    )
    updated_at: datetime | None = Field(
        default=None,
        sa_column=Column(
            DateTime(timezone=True),
            server_default=func.now(),
            onupdate=func.now(),
            nullable=False,
        ),
    )


# Canonical purpose codes used by AI gates.
PURPOSE_AI_DEEPSEEK = "ai_deepseek"
PURPOSE_AI_REKOGNITION = "ai_rekognition"
PURPOSE_MEDIA_PROCESSING = "media_processing"

KNOWN_PURPOSES = frozenset(
    {PURPOSE_AI_DEEPSEEK, PURPOSE_AI_REKOGNITION, PURPOSE_MEDIA_PROCESSING}
)
