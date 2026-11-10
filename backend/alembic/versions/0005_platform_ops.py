"""Platform ops tables: audit, break-glass, consent, webhook outbox.

Revision ID: 0005_platform_ops
Revises: 0004_billing_stripe
Create Date: 2026-11-10

Workstream A — HIPAA-ready scaffolding (chains after billing 0004):
- audit_events: append-only who/what/when (opaque ids, no PHI payloads)
- break_glass_grants: time-boxed emergency access with mandatory reason_code
- consent_records: per-subject purpose flags gating DeepSeek/Rekognition
- webhook_outbox: durable outbound events with retry + HMAC at delivery time
"""

from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

revision: str = "0005_platform_ops"
down_revision: Union[str, None] = "0004_billing_stripe"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.create_table(
        "audit_events",
        sa.Column("id", sa.String(length=36), primary_key=True),
        sa.Column("actor_user_id", sa.String(length=36), nullable=True),
        sa.Column("action", sa.String(length=64), nullable=False),
        sa.Column("resource_type", sa.String(length=64), nullable=False),
        sa.Column("resource_id", sa.String(length=64), nullable=True),
        sa.Column("request_id", sa.String(length=64), nullable=True),
        sa.Column("metadata_json", sa.Text(), nullable=True),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            server_default=sa.func.now(),
            nullable=False,
        ),
    )
    op.create_index("ix_audit_events_actor_user_id", "audit_events", ["actor_user_id"])
    op.create_index("ix_audit_events_action", "audit_events", ["action"])
    op.create_index("ix_audit_events_resource_type", "audit_events", ["resource_type"])
    op.create_index("ix_audit_events_resource_id", "audit_events", ["resource_id"])
    op.create_index("ix_audit_events_request_id", "audit_events", ["request_id"])

    op.create_table(
        "break_glass_grants",
        sa.Column("id", sa.String(length=36), primary_key=True),
        sa.Column("granted_by_user_id", sa.String(length=36), nullable=False),
        sa.Column("target_user_id", sa.String(length=36), nullable=False),
        sa.Column("reason_code", sa.String(length=64), nullable=False),
        sa.Column("ticket_ref", sa.String(length=64), nullable=True),
        sa.Column("reason_detail", sa.Text(), nullable=True),
        sa.Column("expires_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("revoked_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            server_default=sa.func.now(),
            nullable=False,
        ),
    )
    op.create_index(
        "ix_break_glass_grants_granted_by_user_id",
        "break_glass_grants",
        ["granted_by_user_id"],
    )
    op.create_index(
        "ix_break_glass_grants_target_user_id",
        "break_glass_grants",
        ["target_user_id"],
    )
    op.create_index(
        "ix_break_glass_grants_reason_code",
        "break_glass_grants",
        ["reason_code"],
    )
    op.create_index(
        "ix_break_glass_grants_expires_at",
        "break_glass_grants",
        ["expires_at"],
    )

    op.create_table(
        "consent_records",
        sa.Column("id", sa.String(length=36), primary_key=True),
        sa.Column("subject_user_id", sa.String(length=36), nullable=False),
        sa.Column("purpose", sa.String(length=64), nullable=False),
        sa.Column("granted", sa.Boolean(), nullable=False, server_default=sa.false()),
        sa.Column("granted_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("revoked_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("updated_by_user_id", sa.String(length=36), nullable=True),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            server_default=sa.func.now(),
            nullable=False,
        ),
        sa.Column(
            "updated_at",
            sa.DateTime(timezone=True),
            server_default=sa.func.now(),
            nullable=False,
        ),
        sa.UniqueConstraint("subject_user_id", "purpose", name="uq_consent_subject_purpose"),
    )
    op.create_index(
        "ix_consent_records_subject_user_id",
        "consent_records",
        ["subject_user_id"],
    )
    op.create_index("ix_consent_records_purpose", "consent_records", ["purpose"])

    op.create_table(
        "webhook_outbox",
        sa.Column("id", sa.String(length=36), primary_key=True),
        sa.Column("event_type", sa.String(length=64), nullable=False),
        sa.Column("destination_url", sa.String(length=1024), nullable=False),
        sa.Column("payload_json", sa.Text(), nullable=False),
        sa.Column("status", sa.String(length=32), nullable=False, server_default="pending"),
        sa.Column("attempts", sa.Integer(), nullable=False, server_default="0"),
        sa.Column("max_attempts", sa.Integer(), nullable=False, server_default="8"),
        sa.Column("next_attempt_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("last_status_code", sa.Integer(), nullable=True),
        sa.Column("last_error_code", sa.String(length=64), nullable=True),
        sa.Column("delivered_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            server_default=sa.func.now(),
            nullable=False,
        ),
        sa.Column(
            "updated_at",
            sa.DateTime(timezone=True),
            server_default=sa.func.now(),
            nullable=False,
        ),
    )
    op.create_index("ix_webhook_outbox_event_type", "webhook_outbox", ["event_type"])
    op.create_index("ix_webhook_outbox_status", "webhook_outbox", ["status"])
    op.create_index(
        "ix_webhook_outbox_next_attempt_at",
        "webhook_outbox",
        ["next_attempt_at"],
    )


def downgrade() -> None:
    op.drop_table("webhook_outbox")
    op.drop_table("consent_records")
    op.drop_table("break_glass_grants")
    op.drop_table("audit_events")
