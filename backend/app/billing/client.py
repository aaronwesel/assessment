"""
Stripe SDK wrapper — always use StripeClient (never global stripe.api_key).

Test-mode keys only in prep (`sk_test_` / `rk_test_` / `pk_test_`). Prefer a
restricted API key (RAK) with least privilege for Checkout + Customers +
Subscriptions + Billing Portal in non-local environments.
"""

from __future__ import annotations

from functools import lru_cache
from typing import Any

import stripe
from stripe import StripeClient

from app.core.config import Settings, get_settings


class StripeNotConfiguredError(RuntimeError):
    """Raised when required Stripe env placeholders are empty."""


def require_stripe_secret(settings: Settings | None = None) -> str:
    cfg = settings or get_settings()
    key = (cfg.stripe_secret_key or "").strip()
    if not key:
        raise StripeNotConfiguredError("STRIPE_SECRET_KEY is not configured")
    return key


def require_webhook_secret(settings: Settings | None = None) -> str:
    cfg = settings or get_settings()
    secret = (cfg.stripe_webhook_secret or "").strip()
    if not secret:
        raise StripeNotConfiguredError("STRIPE_WEBHOOK_SECRET is not configured")
    return secret


@lru_cache
def get_stripe_client() -> StripeClient:
    """Process-scoped StripeClient bound to the configured secret/RAK."""
    return StripeClient(require_stripe_secret())


def clear_stripe_client_cache() -> None:
    """Test helper — drop cached client after env changes."""
    get_stripe_client.cache_clear()


def construct_webhook_event(payload: bytes, sig_header: str, webhook_secret: str) -> stripe.Event:
    """Verify Stripe-Signature and parse the event (raises SignatureVerificationError)."""
    return stripe.Webhook.construct_event(payload, sig_header, webhook_secret)


def create_customer(*, email: str | None, user_id: str, metadata: dict[str, str] | None = None) -> Any:
    client = get_stripe_client()
    params: dict[str, Any] = {
        "metadata": {"user_id": user_id, **(metadata or {})},
    }
    if email:
        params["email"] = email
    return client.v1.customers.create(params=params)


def create_checkout_session(
    *,
    customer_id: str,
    price_id: str,
    success_url: str,
    cancel_url: str,
    user_id: str,
    integration_identifier: str,
) -> Any:
    """
    Subscription Checkout Session.

    Intentionally omits `payment_method_types` so Stripe dynamic payment methods apply.
    """
    client = get_stripe_client()
    return client.v1.checkout.sessions.create(
        params={
            "mode": "subscription",
            "customer": customer_id,
            "line_items": [{"price": price_id, "quantity": 1}],
            "success_url": success_url,
            "cancel_url": cancel_url,
            "client_reference_id": user_id,
            "metadata": {"user_id": user_id},
            "subscription_data": {"metadata": {"user_id": user_id}},
            "integration_identifier": integration_identifier,
        }
    )


def create_portal_session(*, customer_id: str, return_url: str) -> Any:
    client = get_stripe_client()
    return client.v1.billing_portal.sessions.create(
        params={
            "customer": customer_id,
            "return_url": return_url,
        }
    )


def retrieve_subscription(subscription_id: str) -> Any:
    client = get_stripe_client()
    return client.v1.subscriptions.retrieve(subscription_id)
