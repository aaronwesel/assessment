"""
Stripe webhook fulfillment with signature verification + idempotency.

Subscription entitlements are updated here (and only here for async flows).
Never log raw event payloads — they can contain customer email / address PII.
"""

from __future__ import annotations

from typing import Any

import stripe
from sqlalchemy.exc import IntegrityError
from sqlmodel import Session, select

from app.billing import client as stripe_client
from app.billing import service as billing_service
from app.billing.models import BillingCustomer, StripeWebhookEvent
from app.core.logging import get_logger, log_event

logger = get_logger(__name__)

# Lifecycle + Checkout fulfillment + risk signals we act on (not "subscribe to everything").
HANDLED_EVENT_TYPES = frozenset(
    {
        "checkout.session.completed",
        "checkout.session.async_payment_succeeded",
        "customer.subscription.created",
        "customer.subscription.updated",
        "customer.subscription.deleted",
        "invoice.paid",
        "invoice.payment_failed",
        "charge.dispute.created",
        "charge.refunded",
        "radar.early_fraud_warning.created",
        "customer.updated",
    }
)


class WebhookVerificationError(Exception):
    """Invalid Stripe-Signature or missing header."""


def verify_and_parse_event(payload: bytes, sig_header: str | None, webhook_secret: str) -> stripe.Event:
    if not sig_header:
        raise WebhookVerificationError("Missing Stripe-Signature header")
    try:
        return stripe_client.construct_webhook_event(payload, sig_header, webhook_secret)
    except stripe.error.SignatureVerificationError as exc:
        raise WebhookVerificationError("Invalid Stripe signature") from exc
    except Exception as exc:  # noqa: BLE001 — Stripe may raise ValueError on bad payload
        raise WebhookVerificationError("Invalid webhook payload") from exc


def record_event_once(session: Session, *, event_id: str, event_type: str) -> bool:
    """
    Insert idempotency row. Returns True if this delivery should be processed,
    False if the event_id was already seen (safe 200 duplicate ack).
    """
    existing = session.exec(
        select(StripeWebhookEvent).where(StripeWebhookEvent.stripe_event_id == event_id)
    ).first()
    if existing is not None:
        return False
    session.add(
        StripeWebhookEvent(
            stripe_event_id=event_id,
            event_type=event_type,
            status="processing",
        )
    )
    try:
        session.commit()
    except IntegrityError:
        session.rollback()
        return False
    return True


def mark_event_processed(session: Session, event_id: str, *, detail: str | None = None) -> None:
    row = session.exec(
        select(StripeWebhookEvent).where(StripeWebhookEvent.stripe_event_id == event_id)
    ).first()
    if row is None:
        return
    row.status = "processed"
    if detail:
        row.detail = detail[:500]
    session.add(row)
    session.commit()


def process_stripe_event(session: Session, event: stripe.Event) -> str:
    """Dispatch by type; returns a short opaque processing code for logs/ack."""
    event_type = event["type"] if isinstance(event, dict) else event.type
    data_object = event["data"]["object"] if isinstance(event, dict) else event.data.object

    if event_type not in HANDLED_EVENT_TYPES:
        log_event(logger, "billing_webhook_ignored", event_type=event_type)
        return "ignored"

    if event_type in {"checkout.session.completed", "checkout.session.async_payment_succeeded"}:
        return _handle_checkout_session(session, data_object, event_type=event_type)

    if event_type.startswith("customer.subscription."):
        return _handle_subscription(session, data_object, event_type=event_type)

    if event_type in {"invoice.paid", "invoice.payment_failed"}:
        return _handle_invoice(session, data_object, event_type=event_type)

    if event_type in {
        "charge.dispute.created",
        "charge.refunded",
        "radar.early_fraud_warning.created",
    }:
        # Risk path: log opaque codes only; ops follow up in Stripe Dashboard.
        log_event(logger, "billing_risk_event", event_type=event_type)
        return "risk_noted"

    if event_type == "customer.updated":
        return _handle_customer_updated(session, data_object)

    return "ignored"


def _handle_checkout_session(session: Session, checkout: Any, *, event_type: str) -> str:
    payment_status = _get(checkout, "payment_status")
    mode = _get(checkout, "mode")
    # Gate fulfillment on payment_status for both sync and async success events.
    if payment_status not in {"paid", "no_payment_required"}:
        log_event(logger, "billing_checkout_pending", event_type=event_type)
        return "checkout_pending"

    if mode != "subscription":
        return "checkout_non_subscription"

    user_id = _get(checkout, "client_reference_id") or _meta(checkout, "user_id")
    customer_id = _get(checkout, "customer")
    subscription_id = _get(checkout, "subscription")

    if not user_id or not customer_id:
        log_event(logger, "billing_checkout_missing_refs", event_type=event_type)
        return "checkout_missing_refs"

    customer = _ensure_local_customer(session, user_id=str(user_id), stripe_customer_id=str(customer_id))
    if subscription_id:
        sub = stripe_client.retrieve_subscription(str(subscription_id))
        billing_service.upsert_subscription_from_stripe(
            session,
            user_id=str(user_id),
            billing_customer_id=customer.id,
            stripe_subscription=sub,
        )
    log_event(logger, "billing_checkout_fulfilled", event_type=event_type)
    return "checkout_fulfilled"


def _handle_subscription(session: Session, subscription: Any, *, event_type: str) -> str:
    stripe_customer_id = _get(subscription, "customer")
    user_id = _meta(subscription, "user_id")
    customer = None
    if stripe_customer_id:
        customer = billing_service.find_customer_by_stripe_id(session, str(stripe_customer_id))
    if customer is None and user_id:
        customer = _ensure_local_customer(
            session, user_id=str(user_id), stripe_customer_id=str(stripe_customer_id or "")
        )
    if customer is None:
        log_event(logger, "billing_subscription_orphan", event_type=event_type)
        return "subscription_orphan"

    billing_service.upsert_subscription_from_stripe(
        session,
        user_id=customer.user_id,
        billing_customer_id=customer.id,
        stripe_subscription=subscription,
    )
    log_event(logger, "billing_subscription_synced", event_type=event_type)
    return "subscription_synced"


def _handle_invoice(session: Session, invoice: Any, *, event_type: str) -> str:
    subscription_id = _get(invoice, "subscription")
    if not subscription_id:
        return "invoice_no_subscription"
    try:
        sub = stripe_client.retrieve_subscription(str(subscription_id))
    except Exception:  # noqa: BLE001
        log_event(logger, "billing_invoice_sub_fetch_failed", event_type=event_type)
        return "invoice_sub_fetch_failed"
    return _handle_subscription(session, sub, event_type=event_type)


def _handle_customer_updated(session: Session, customer_obj: Any) -> str:
    stripe_customer_id = _get(customer_obj, "id")
    if not stripe_customer_id:
        return "customer_missing_id"
    local = billing_service.find_customer_by_stripe_id(session, str(stripe_customer_id))
    if local is None:
        return "customer_unknown"
    # Keep email snapshot in sync when present; never log the value.
    email = _get(customer_obj, "email")
    if email and email != local.email:
        local.email = str(email)[:320]
        session.add(local)
        session.commit()
    log_event(logger, "billing_customer_updated")
    return "customer_updated"


def _ensure_local_customer(
    session: Session, *, user_id: str, stripe_customer_id: str
) -> BillingCustomer:
    existing = billing_service.get_billing_customer(session, user_id)
    if existing:
        if stripe_customer_id and existing.stripe_customer_id != stripe_customer_id:
            existing.stripe_customer_id = stripe_customer_id
            session.add(existing)
            session.commit()
            session.refresh(existing)
        return existing
    by_stripe = (
        billing_service.find_customer_by_stripe_id(session, stripe_customer_id)
        if stripe_customer_id
        else None
    )
    if by_stripe:
        return by_stripe
    customer = BillingCustomer(user_id=user_id, stripe_customer_id=stripe_customer_id)
    session.add(customer)
    session.commit()
    session.refresh(customer)
    return customer


def _get(obj: Any, key: str) -> Any:
    if obj is None:
        return None
    if isinstance(obj, dict):
        return obj.get(key)
    return getattr(obj, key, None)


def _meta(obj: Any, key: str) -> Any:
    metadata = _get(obj, "metadata") or {}
    if isinstance(metadata, dict):
        return metadata.get(key)
    return getattr(metadata, key, None)
