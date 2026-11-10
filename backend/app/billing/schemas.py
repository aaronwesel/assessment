"""Pydantic request/response models for billing routes."""

from __future__ import annotations

from datetime import datetime

from pydantic import BaseModel, Field


class BillingConfigResponse(BaseModel):
    """Publishable key is safe for the browser; secrets stay server-side."""

    configured: bool
    publishable_key: str = ""
    price_configured: bool = False


class BillingStatusResponse(BaseModel):
    has_customer: bool = False
    stripe_customer_id: str | None = None
    subscription_status: str = "none"
    stripe_subscription_id: str | None = None
    stripe_price_id: str | None = None
    cancel_at_period_end: bool = False
    current_period_end: datetime | None = None


class BillingSummaryResponse(BaseModel):
    """Admin skim consumed by superadmin console (`/api/v1/billing/summary`)."""

    status: str = "ok"
    configured: bool = False
    customer_count: int = 0
    active_subscription_count: int = 0
    # Echo fields for the caller's own mirror (handy when summary is used as /me fallback).
    subscription_status: str = "none"
    customer_id: str | None = None
    plan: str | None = None


class CheckoutSessionRequest(BaseModel):
    """Optional overrides; defaults come from settings (STRIPE_PRICE_ID + app URLs)."""

    success_url: str | None = Field(default=None, max_length=2048)
    cancel_url: str | None = Field(default=None, max_length=2048)


class CheckoutSessionResponse(BaseModel):
    session_id: str
    url: str


class PortalSessionRequest(BaseModel):
    return_url: str | None = Field(default=None, max_length=2048)


class PortalSessionResponse(BaseModel):
    url: str


class WebhookAckResponse(BaseModel):
    received: bool = True
    duplicate: bool = False
    event_type: str | None = None
