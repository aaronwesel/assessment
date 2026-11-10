"""
Billing domain logic: ensure Stripe Customer, start Checkout/Portal, sync status.

Fulfillment of paid subscriptions happens in `webhooks.py` — never trust the
Checkout success page alone for entitlement.
"""

from __future__ import annotations

import secrets
import string
from datetime import datetime, timezone
from typing import Any

from sqlmodel import Session, select

from app.billing import client as stripe_client
from app.billing.models import BillingCustomer, BillingSubscription, SubscriptionStatus
from app.core.config import Settings, get_settings
from app.core.logging import get_logger, log_event
from app.models.user import User

logger = get_logger(__name__)


class BillingServiceError(Exception):
    """User-facing billing failure (mapped to 4xx by routes)."""


def _integration_identifier() -> str:
    """Tag Checkout flows in the Dashboard (API ≥ 2026-03-25); 8 random letters suffix."""
    suffix = "".join(secrets.choice(string.ascii_lowercase) for _ in range(8))
    return f"assessment_sub_{suffix}"


def billing_configured(settings: Settings | None = None) -> bool:
    cfg = settings or get_settings()
    return bool((cfg.stripe_secret_key or "").strip() and (cfg.stripe_price_id or "").strip())


def get_user_email(session: Session, user_id: str) -> str | None:
    user = session.get(User, user_id)
    return user.email if user else None


def get_billing_customer(session: Session, user_id: str) -> BillingCustomer | None:
    return session.exec(select(BillingCustomer).where(BillingCustomer.user_id == user_id)).first()


def get_primary_subscription(session: Session, user_id: str) -> BillingSubscription | None:
    rows = list(
        session.exec(select(BillingSubscription).where(BillingSubscription.user_id == user_id))
    )
    if not rows:
        return None
    # Prefer active/trialing; else most recently updated.
    priority = {
        SubscriptionStatus.ACTIVE.value: 0,
        SubscriptionStatus.TRIALING.value: 1,
        SubscriptionStatus.PAST_DUE.value: 2,
        SubscriptionStatus.PAUSED.value: 3,
    }
    rows.sort(key=lambda r: (priority.get(r.status, 9), r.updated_at or datetime.min), reverse=False)
    return rows[0]


def ensure_stripe_customer(session: Session, *, user_id: str, email: str | None) -> BillingCustomer:
    existing = get_billing_customer(session, user_id)
    if existing:
        return existing

    created = stripe_client.create_customer(email=email, user_id=user_id)
    customer = BillingCustomer(
        user_id=user_id,
        stripe_customer_id=created["id"],
        email=email,
    )
    session.add(customer)
    session.commit()
    session.refresh(customer)
    log_event(logger, "billing_customer_created", user_id=user_id)
    return customer


def status_for_user(session: Session, user_id: str) -> dict[str, Any]:
    customer = get_billing_customer(session, user_id)
    sub = get_primary_subscription(session, user_id)
    return {
        "has_customer": customer is not None,
        "stripe_customer_id": customer.stripe_customer_id if customer else None,
        "subscription_status": sub.status if sub else SubscriptionStatus.NONE.value,
        "stripe_subscription_id": sub.stripe_subscription_id if sub else None,
        "stripe_price_id": sub.stripe_price_id if sub else None,
        "cancel_at_period_end": bool(sub.cancel_at_period_end) if sub else False,
        "current_period_end": sub.current_period_end if sub else None,
    }


def create_checkout_session_for_user(
    session: Session,
    *,
    user_id: str,
    success_url: str | None = None,
    cancel_url: str | None = None,
    settings: Settings | None = None,
) -> dict[str, str]:
    cfg = settings or get_settings()
    if not billing_configured(cfg):
        raise BillingServiceError("Stripe billing is not configured")

    price_id = cfg.stripe_price_id.strip()
    email = get_user_email(session, user_id)
    customer = ensure_stripe_customer(session, user_id=user_id, email=email)

    success = (success_url or cfg.stripe_checkout_success_url).strip()
    cancel = (cancel_url or cfg.stripe_checkout_cancel_url).strip()
    if not success or not cancel:
        raise BillingServiceError("Checkout success/cancel URLs are not configured")

    checkout = stripe_client.create_checkout_session(
        customer_id=customer.stripe_customer_id,
        price_id=price_id,
        success_url=success,
        cancel_url=cancel,
        user_id=user_id,
        integration_identifier=_integration_identifier(),
    )
    url = checkout.get("url") if isinstance(checkout, dict) else getattr(checkout, "url", None)
    session_id = checkout.get("id") if isinstance(checkout, dict) else getattr(checkout, "id", None)
    if not url or not session_id:
        raise BillingServiceError("Stripe Checkout Session missing url")
    log_event(logger, "billing_checkout_session_created", user_id=user_id)
    return {"session_id": session_id, "url": url}


def create_portal_session_for_user(
    session: Session,
    *,
    user_id: str,
    return_url: str | None = None,
    settings: Settings | None = None,
) -> dict[str, str]:
    cfg = settings or get_settings()
    if not (cfg.stripe_secret_key or "").strip():
        raise BillingServiceError("Stripe billing is not configured")

    customer = get_billing_customer(session, user_id)
    if customer is None:
        raise BillingServiceError("No Stripe customer for this account; start Checkout first")

    ret = (return_url or cfg.stripe_portal_return_url).strip()
    if not ret:
        raise BillingServiceError("Customer Portal return URL is not configured")

    portal = stripe_client.create_portal_session(
        customer_id=customer.stripe_customer_id,
        return_url=ret,
    )
    url = portal.get("url") if isinstance(portal, dict) else getattr(portal, "url", None)
    if not url:
        raise BillingServiceError("Stripe Customer Portal session missing url")
    log_event(logger, "billing_portal_session_created", user_id=user_id)
    return {"url": url}


def upsert_subscription_from_stripe(
    session: Session,
    *,
    user_id: str,
    billing_customer_id: str,
    stripe_subscription: Any,
) -> BillingSubscription:
    """Mirror a Stripe Subscription object into local state (webhook-driven)."""
    sub_id = _obj_get(stripe_subscription, "id")
    status = _obj_get(stripe_subscription, "status") or SubscriptionStatus.INCOMPLETE.value
    cancel_at_period_end = bool(_obj_get(stripe_subscription, "cancel_at_period_end") or False)
    period_end_ts = _obj_get(stripe_subscription, "current_period_end")
    period_end = (
        datetime.fromtimestamp(int(period_end_ts), tz=timezone.utc) if period_end_ts else None
    )
    price_id = _extract_price_id(stripe_subscription)

    existing = session.exec(
        select(BillingSubscription).where(BillingSubscription.stripe_subscription_id == sub_id)
    ).first()
    if existing is None:
        existing = BillingSubscription(
            user_id=user_id,
            billing_customer_id=billing_customer_id,
            stripe_subscription_id=sub_id,
        )
        session.add(existing)

    existing.user_id = user_id
    existing.billing_customer_id = billing_customer_id
    existing.status = str(status)
    existing.cancel_at_period_end = cancel_at_period_end
    existing.current_period_end = period_end
    if price_id:
        existing.stripe_price_id = price_id
    session.add(existing)
    session.commit()
    session.refresh(existing)
    return existing


def find_customer_by_stripe_id(session: Session, stripe_customer_id: str) -> BillingCustomer | None:
    return session.exec(
        select(BillingCustomer).where(BillingCustomer.stripe_customer_id == stripe_customer_id)
    ).first()


def _obj_get(obj: Any, key: str) -> Any:
    if obj is None:
        return None
    if isinstance(obj, dict):
        return obj.get(key)
    return getattr(obj, key, None)


def _extract_price_id(stripe_subscription: Any) -> str | None:
    items = _obj_get(stripe_subscription, "items")
    data = None
    if isinstance(items, dict):
        data = items.get("data")
    else:
        data = _obj_get(items, "data") if items is not None else None
    if not data:
        return None
    first = data[0]
    price = _obj_get(first, "price")
    if isinstance(price, str):
        return price
    return _obj_get(price, "id") if price is not None else None
