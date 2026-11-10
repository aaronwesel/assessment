"""Stripe billing tables (customers, subscriptions, webhook idempotency).

Revision ID: 0004_billing_stripe
Revises: 0003_mfa_fields
Create Date: 2026-11-10

Additive billing revision after MFA. If platform ops lands additional
revisions first, rebase this chain onto the new head rather than rewriting
platform migrations.
"""

from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

revision: str = "0004_billing_stripe"
down_revision: Union[str, None] = "0003_mfa_fields"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.create_table(
        "billing_customers",
        sa.Column("id", sa.String(length=36), nullable=False),
        sa.Column("user_id", sa.String(length=36), nullable=False),
        sa.Column("stripe_customer_id", sa.String(length=255), nullable=False),
        sa.Column("email", sa.String(length=320), nullable=True),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            server_default=sa.text("CURRENT_TIMESTAMP"),
            nullable=False,
        ),
        sa.Column(
            "updated_at",
            sa.DateTime(timezone=True),
            server_default=sa.text("CURRENT_TIMESTAMP"),
            nullable=False,
        ),
        sa.PrimaryKeyConstraint("id"),
        sa.UniqueConstraint("user_id", name="uq_billing_customers_user_id"),
        sa.UniqueConstraint("stripe_customer_id"),
    )
    op.create_index("ix_billing_customers_user_id", "billing_customers", ["user_id"])
    op.create_index(
        "ix_billing_customers_stripe_customer_id",
        "billing_customers",
        ["stripe_customer_id"],
    )

    op.create_table(
        "billing_subscriptions",
        sa.Column("id", sa.String(length=36), nullable=False),
        sa.Column("user_id", sa.String(length=36), nullable=False),
        sa.Column("billing_customer_id", sa.String(length=36), nullable=False),
        sa.Column("stripe_subscription_id", sa.String(length=255), nullable=False),
        sa.Column("stripe_price_id", sa.String(length=255), nullable=True),
        sa.Column("status", sa.String(length=64), nullable=False),
        sa.Column("cancel_at_period_end", sa.Boolean(), nullable=False, server_default=sa.false()),
        sa.Column("current_period_end", sa.DateTime(timezone=True), nullable=True),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            server_default=sa.text("CURRENT_TIMESTAMP"),
            nullable=False,
        ),
        sa.Column(
            "updated_at",
            sa.DateTime(timezone=True),
            server_default=sa.text("CURRENT_TIMESTAMP"),
            nullable=False,
        ),
        sa.PrimaryKeyConstraint("id"),
        sa.UniqueConstraint(
            "stripe_subscription_id",
            name="uq_billing_subscriptions_stripe_id",
        ),
    )
    op.create_index("ix_billing_subscriptions_user_id", "billing_subscriptions", ["user_id"])
    op.create_index(
        "ix_billing_subscriptions_billing_customer_id",
        "billing_subscriptions",
        ["billing_customer_id"],
    )
    op.create_index(
        "ix_billing_subscriptions_stripe_subscription_id",
        "billing_subscriptions",
        ["stripe_subscription_id"],
    )
    op.create_index("ix_billing_subscriptions_status", "billing_subscriptions", ["status"])

    op.create_table(
        "stripe_webhook_events",
        sa.Column("id", sa.String(length=36), nullable=False),
        sa.Column("stripe_event_id", sa.String(length=255), nullable=False),
        sa.Column("event_type", sa.String(length=128), nullable=False),
        sa.Column("status", sa.String(length=32), nullable=False),
        sa.Column("detail", sa.Text(), nullable=True),
        sa.Column(
            "processed_at",
            sa.DateTime(timezone=True),
            server_default=sa.text("CURRENT_TIMESTAMP"),
            nullable=False,
        ),
        sa.PrimaryKeyConstraint("id"),
        sa.UniqueConstraint("stripe_event_id"),
    )
    op.create_index(
        "ix_stripe_webhook_events_stripe_event_id",
        "stripe_webhook_events",
        ["stripe_event_id"],
    )
    op.create_index("ix_stripe_webhook_events_event_type", "stripe_webhook_events", ["event_type"])


def downgrade() -> None:
    op.drop_index("ix_stripe_webhook_events_event_type", table_name="stripe_webhook_events")
    op.drop_index("ix_stripe_webhook_events_stripe_event_id", table_name="stripe_webhook_events")
    op.drop_table("stripe_webhook_events")

    op.drop_index("ix_billing_subscriptions_status", table_name="billing_subscriptions")
    op.drop_index(
        "ix_billing_subscriptions_stripe_subscription_id",
        table_name="billing_subscriptions",
    )
    op.drop_index(
        "ix_billing_subscriptions_billing_customer_id",
        table_name="billing_subscriptions",
    )
    op.drop_index("ix_billing_subscriptions_user_id", table_name="billing_subscriptions")
    op.drop_table("billing_subscriptions")

    op.drop_index("ix_billing_customers_stripe_customer_id", table_name="billing_customers")
    op.drop_index("ix_billing_customers_user_id", table_name="billing_customers")
    op.drop_table("billing_customers")
