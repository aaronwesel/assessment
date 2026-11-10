"""
Break-glass / emergency-access grants — time-boxed overrides with mandatory reason.

HIPAA-minded design:
- `reason_code` is a short snake_case token (ops taxonomy), not free-text clinical notes.
- `target_user_id` / `granted_by_user_id` are opaque UUIDs.
- Grants auto-expire; revoke sets `revoked_at` without deleting the row (auditability).
"""

from __future__ import annotations

from datetime import datetime
from uuid import uuid4

from sqlalchemy import Column, DateTime, Text, func
from sqlmodel import Field, SQLModel


class BreakGlassGrant(SQLModel, table=True):
    __tablename__ = "break_glass_grants"

    id: str = Field(default_factory=lambda: str(uuid4()), primary_key=True)
    granted_by_user_id: str = Field(max_length=36, index=True)
    target_user_id: str = Field(max_length=36, index=True)
    # Short taxonomy code, e.g. mfa_recovery, suspected_compromise.
    reason_code: str = Field(max_length=64, index=True)
    # Optional opaque ticket/ref id from an external ITSM — never patient MRNs.
    ticket_ref: str | None = Field(default=None, max_length=64)
    # Human-readable ops note stripped of PHI by the service layer before persist.
    reason_detail: str | None = Field(default=None, sa_column=Column(Text, nullable=True))
    expires_at: datetime = Field(
        sa_column=Column(DateTime(timezone=True), nullable=False, index=True),
    )
    revoked_at: datetime | None = Field(
        default=None,
        sa_column=Column(DateTime(timezone=True), nullable=True),
    )
    created_at: datetime | None = Field(
        default=None,
        sa_column=Column(DateTime(timezone=True), server_default=func.now(), nullable=False),
    )
