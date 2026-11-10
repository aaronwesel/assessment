"""
Local mirrors of Stripe Customer / Subscription + webhook idempotency ledger.

HIPAA notes:
- No PHI columns. Link only by opaque user_id and Stripe ids.
- Email is duplicated from the auth account for Stripe Customer create only;
  do not log it; prefer stripe_* ids in ops tooling.
"""

from __future__ import annotations

from datetime import datetime
from enum import Enum
from typing import Optional
from uuid import uuid4

from sqlalchemy import Column, DateTime, Text, UniqueConstraint, func
from sqlmodel import Field, SQLModel


class SubscriptionStatus(str, Enum):
    """Subset of Stripe subscription statuses we surface in the UI."""

    INCOMPLETE = "incomplete"
    INCOMPLETE_EXPIRED = "incomplete_expired"
    TRIALING = "trialing"
    ACTIVE = "active"
    PAST_DUE = "past_due"
    CANCELED = "canceled"
    UNPAID = "unpaid"
    PAUSED = "paused"
    NONE = "none"


class BillingCustomer(SQLModel, table=True):
    """One Stripe Customer per local user (prep: per-user SaaS billing)."""

    __tablename__ = "billing_customers"
    __table_args__ = (UniqueConstraint("user_id", name="uq_billing_customers_user_id"),)

    id: str = Field(default_factory=lambda: str(uuid4()), primary_key=True)
    user_id: str = Field(max_length=36, index=True)
    stripe_customer_id: str = Field(max_length=255, unique=True, index=True)
    # Account email snapshot for Stripe Customer create; not clinical PHI.
    email: Optional[str] = Field(default=None, max_length=320)
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


class BillingSubscription(SQLModel, table=True):
    """Local copy of the user's primary Stripe Subscription."""

    __tablename__ = "billing_subscriptions"
    __table_args__ = (
        UniqueConstraint("stripe_subscription_id", name="uq_billing_subscriptions_stripe_id"),
    )

    id: str = Field(default_factory=lambda: str(uuid4()), primary_key=True)
    user_id: str = Field(max_length=36, index=True)
    billing_customer_id: str = Field(max_length=36, index=True)
    stripe_subscription_id: str = Field(max_length=255, index=True)
    stripe_price_id: Optional[str] = Field(default=None, max_length=255)
    status: str = Field(default=SubscriptionStatus.INCOMPLETE.value, max_length=64, index=True)
    cancel_at_period_end: bool = Field(default=False)
    current_period_end: datetime | None = Field(
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


class CrawlJobUsage(SQLModel, table=True):
    """Idempotent per-job crawl meter ledger (pages-with-results only)."""

    __tablename__ = "crawl_job_usage"
    __table_args__ = (
        UniqueConstraint("user_id", "crawl_job_id", name="uq_crawl_job_usage_user_job"),
    )

    id: str = Field(default_factory=lambda: str(uuid4()), primary_key=True)
    user_id: str = Field(max_length=36, index=True)
    crawl_job_id: str = Field(max_length=64, index=True)
    pages_with_results: int = Field(default=0)
    billable_pages: int = Field(default=0)
    stripe_meter_reported: bool = Field(default=False)
    created_at: datetime | None = Field(
        default=None,
        sa_column=Column(DateTime(timezone=True), server_default=func.now(), nullable=False),
    )


class StripeWebhookEvent(SQLModel, table=True):
    """Idempotency ledger — Stripe may redeliver the same event_id."""

    __tablename__ = "stripe_webhook_events"

    id: str = Field(default_factory=lambda: str(uuid4()), primary_key=True)
    stripe_event_id: str = Field(max_length=255, unique=True, index=True)
    event_type: str = Field(max_length=128, index=True)
    # Opaque processing note only (event codes); never store payloads (may contain PII).
    status: str = Field(default="processed", max_length=32)
    detail: Optional[str] = Field(default=None, sa_column=Column(Text, nullable=True))
    processed_at: datetime | None = Field(
        default=None,
        sa_column=Column(DateTime(timezone=True), server_default=func.now(), nullable=False),
    )
