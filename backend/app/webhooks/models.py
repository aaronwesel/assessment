"""
Durable webhook outbox rows — transactional enqueue, async delivery/retry.

Pattern: API writes business change + outbox row in one DB commit; a worker
(or `/webhooks/outbox/process` admin hook in this stub) delivers with HMAC
and exponential backoff. Prevents lost events when the remote is briefly down.
"""

from __future__ import annotations

from datetime import datetime
from uuid import uuid4

from sqlalchemy import Column, DateTime, Text, func
from sqlmodel import Field, SQLModel


class WebhookOutbox(SQLModel, table=True):
    __tablename__ = "webhook_outbox"

    id: str = Field(default_factory=lambda: str(uuid4()), primary_key=True)
    event_type: str = Field(max_length=64, index=True)
    # Destination URL — ops-configured; treat as sensitive in logs (host only).
    destination_url: str = Field(max_length=1024)
    # JSON payload — codes/ids only; PHI forbidden at enqueue time.
    payload_json: str = Field(sa_column=Column(Text, nullable=False))
    # pending | delivering | delivered | failed | dead
    status: str = Field(default="pending", max_length=32, index=True)
    attempts: int = Field(default=0)
    max_attempts: int = Field(default=8)
    next_attempt_at: datetime | None = Field(
        default=None,
        sa_column=Column(DateTime(timezone=True), nullable=True, index=True),
    )
    last_status_code: int | None = Field(default=None)
    last_error_code: str | None = Field(default=None, max_length=64)
    delivered_at: datetime | None = Field(
        default=None,
        sa_column=Column(DateTime(timezone=True), nullable=True),
    )
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
