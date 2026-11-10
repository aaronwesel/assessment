"""
Immutable audit-log rows — who / what / when without PHI payloads.

Assessment / HIPAA notes:
- Actors and resources are opaque UUIDs (JWT `sub`, resource ids) — never emails.
- `metadata_json` is allowlisted machine codes only; reject free-text PHI at write time.
- No update/delete API: append-only for evidentiary integrity. Retention/purge is an
  ops job outside this scaffold (document in runbooks, not silent DELETE endpoints).
"""

from __future__ import annotations

from datetime import datetime
from uuid import uuid4

from sqlalchemy import Column, DateTime, Text, func
from sqlmodel import Field, SQLModel


class AuditEvent(SQLModel, table=True):
    """Append-only audit record suitable for CSV/JSON export."""

    __tablename__ = "audit_events"

    id: str = Field(default_factory=lambda: str(uuid4()), primary_key=True)
    # Opaque actor id (JWT subject). Null for system/unauthenticated events.
    actor_user_id: str | None = Field(default=None, max_length=36, index=True)
    # Short snake_case action code, e.g. break_glass_grant, consent_granted.
    action: str = Field(max_length=64, index=True)
    resource_type: str = Field(max_length=64, index=True)
    resource_id: str | None = Field(default=None, max_length=64, index=True)
    # Correlation id from X-Request-ID middleware when present.
    request_id: str | None = Field(default=None, max_length=64, index=True)
    # JSON object of non-PHI codes/flags only (string column for SQLite portability).
    metadata_json: str | None = Field(default=None, sa_column=Column(Text, nullable=True))
    created_at: datetime | None = Field(
        default=None,
        sa_column=Column(DateTime(timezone=True), server_default=func.now(), nullable=False),
    )
