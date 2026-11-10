"""Stripe billing routes: config, checkout/portal (mocked), webhook verify + idempotency."""

from __future__ import annotations

import json
import os
from typing import Any
from unittest.mock import MagicMock, patch

import pytest
from fastapi.testclient import TestClient

from app.core.config import get_settings


@pytest.fixture()
def stripe_env(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("STRIPE_SECRET_KEY", "sk_test_dummy_not_real")
    monkeypatch.setenv("STRIPE_PUBLISHABLE_KEY", "pk_test_dummy_not_real")
    monkeypatch.setenv("STRIPE_WEBHOOK_SECRET", "whsec_test_dummy")
    monkeypatch.setenv("STRIPE_PRICE_ID", "price_test_dummy")
    monkeypatch.setenv(
        "STRIPE_CHECKOUT_SUCCESS_URL",
        "http://localhost:5173/billing?checkout=success",
    )
    monkeypatch.setenv(
        "STRIPE_CHECKOUT_CANCEL_URL",
        "http://localhost:5173/billing?checkout=cancel",
    )
    monkeypatch.setenv("STRIPE_PORTAL_RETURN_URL", "http://localhost:5173/billing")
    get_settings.cache_clear()
    from app.billing.client import clear_stripe_client_cache

    clear_stripe_client_cache()
    yield
    get_settings.cache_clear()
    clear_stripe_client_cache()


def _register_and_token(client: TestClient, email: str = "billing@example.com") -> str:
    response = client.post(
        "/api/v1/auth/register",
        json={"email": email, "password": "SecurePass1!x", "full_name": "Billing User"},
    )
    assert response.status_code == 201, response.text
    return response.json()["access_token"]


def test_billing_config_unconfigured(client: TestClient) -> None:
    # Default conftest leaves Stripe env empty.
    response = client.get("/api/v1/billing/config")
    assert response.status_code == 200
    body = response.json()
    assert body["configured"] is False
    assert body["publishable_key"] == ""


def test_billing_config_configured(client: TestClient, stripe_env: None) -> None:
    response = client.get("/api/v1/billing/config")
    assert response.status_code == 200
    body = response.json()
    assert body["configured"] is True
    assert body["publishable_key"].startswith("pk_test_")
    assert body["price_configured"] is True


def test_billing_status_requires_jwt(client: TestClient) -> None:
    assert client.get("/api/v1/billing/status").status_code == 401


def test_billing_status_none(client: TestClient) -> None:
    token = _register_and_token(client, email="billing-status@example.com")
    response = client.get(
        "/api/v1/billing/status",
        headers={"Authorization": f"Bearer {token}"},
    )
    assert response.status_code == 200
    body = response.json()
    assert body["subscription_status"] == "none"
    assert body["has_customer"] is False


def test_checkout_session_mocked(client: TestClient, stripe_env: None) -> None:
    token = _register_and_token(client, email="billing-checkout@example.com")

    fake_customer = {"id": "cus_test_123"}
    fake_session = {"id": "cs_test_123", "url": "https://checkout.stripe.com/c/pay/cs_test_123"}

    with (
        patch("app.billing.service.stripe_client.create_customer", return_value=fake_customer),
        patch(
            "app.billing.service.stripe_client.create_checkout_session",
            return_value=fake_session,
        ) as create_checkout,
    ):
        response = client.post(
            "/api/v1/billing/checkout-session",
            headers={"Authorization": f"Bearer {token}"},
            json={},
        )
    assert response.status_code == 200, response.text
    body = response.json()
    assert body["session_id"] == "cs_test_123"
    assert body["url"].startswith("https://checkout.stripe.com/")
    # Ensure we did not hardcode payment_method_types in the Stripe call kwargs.
    kwargs = create_checkout.call_args.kwargs
    assert "payment_method_types" not in kwargs

    status = client.get(
        "/api/v1/billing/status",
        headers={"Authorization": f"Bearer {token}"},
    )
    assert status.status_code == 200
    assert status.json()["has_customer"] is True
    assert status.json()["stripe_customer_id"] == "cus_test_123"


def test_portal_session_requires_customer(client: TestClient, stripe_env: None) -> None:
    token = _register_and_token(client, email="billing-portal-missing@example.com")
    response = client.post(
        "/api/v1/billing/portal-session",
        headers={"Authorization": f"Bearer {token}"},
        json={},
    )
    assert response.status_code == 400


def test_portal_session_mocked(client: TestClient, stripe_env: None) -> None:
    token = _register_and_token(client, email="billing-portal@example.com")
    with patch(
        "app.billing.service.stripe_client.create_customer",
        return_value={"id": "cus_portal_1"},
    ), patch(
        "app.billing.service.stripe_client.create_checkout_session",
        return_value={"id": "cs_portal", "url": "https://checkout.stripe.com/c/pay/cs_portal"},
    ):
        assert (
            client.post(
                "/api/v1/billing/checkout-session",
                headers={"Authorization": f"Bearer {token}"},
                json={},
            ).status_code
            == 200
        )

    with patch(
        "app.billing.service.stripe_client.create_portal_session",
        return_value={"url": "https://billing.stripe.com/p/session/test"},
    ):
        response = client.post(
            "/api/v1/billing/portal-session",
            headers={"Authorization": f"Bearer {token}"},
            json={},
        )
    assert response.status_code == 200
    assert response.json()["url"].startswith("https://billing.stripe.com/")


def _fake_event(event_id: str, event_type: str, obj: dict[str, Any]) -> dict[str, Any]:
    return {
        "id": event_id,
        "type": event_type,
        "data": {"object": obj},
    }


def test_webhook_rejects_bad_signature(client: TestClient, stripe_env: None) -> None:
    import stripe
    from app.billing.webhooks import WebhookVerificationError

    with patch(
        "app.billing.webhooks.stripe_client.construct_webhook_event",
        side_effect=stripe.error.SignatureVerificationError("bad sig", "sig_header"),
    ):
        response = client.post(
            "/api/v1/billing/webhooks/stripe",
            content=b"{}",
            headers={"Stripe-Signature": "t=1,v1=bad"},
        )
    assert response.status_code == 400

    with patch(
        "app.billing.routes.webhook_service.verify_and_parse_event",
        side_effect=WebhookVerificationError("Invalid Stripe signature"),
    ):
        response = client.post(
            "/api/v1/billing/webhooks/stripe",
            content=b"{}",
            headers={"Stripe-Signature": "t=1,v1=bad"},
        )
    assert response.status_code == 400


def test_webhook_checkout_and_idempotency(client: TestClient, stripe_env: None) -> None:
    token = _register_and_token(client, email="billing-webhook@example.com")
    # Seed a local customer row via checkout mock.
    with patch(
        "app.billing.service.stripe_client.create_customer",
        return_value={"id": "cus_wh_1"},
    ), patch(
        "app.billing.service.stripe_client.create_checkout_session",
        return_value={"id": "cs_wh", "url": "https://checkout.stripe.com/c/pay/cs_wh"},
    ):
        me = client.get("/api/v1/auth/me", headers={"Authorization": f"Bearer {token}"})
        assert me.status_code == 200
        user_id = me.json()["subject"]
        assert (
            client.post(
                "/api/v1/billing/checkout-session",
                headers={"Authorization": f"Bearer {token}"},
                json={},
            ).status_code
            == 200
        )

    checkout_obj = {
        "id": "cs_wh",
        "mode": "subscription",
        "payment_status": "paid",
        "customer": "cus_wh_1",
        "client_reference_id": user_id,
        "subscription": "sub_wh_1",
        "metadata": {"user_id": user_id},
    }
    sub_obj = {
        "id": "sub_wh_1",
        "status": "active",
        "customer": "cus_wh_1",
        "cancel_at_period_end": False,
        "current_period_end": 1_900_000_000,
        "metadata": {"user_id": user_id},
        "items": {"data": [{"price": {"id": "price_test_dummy"}}]},
    }
    event = _fake_event("evt_test_1", "checkout.session.completed", checkout_obj)

    with (
        patch(
            "app.billing.routes.webhook_service.verify_and_parse_event",
            return_value=event,
        ),
        patch(
            "app.billing.webhooks.stripe_client.retrieve_subscription",
            return_value=sub_obj,
        ),
    ):
        first = client.post(
            "/api/v1/billing/webhooks/stripe",
            content=json.dumps(event).encode(),
            headers={"Stripe-Signature": "t=1,v1=test"},
        )
        second = client.post(
            "/api/v1/billing/webhooks/stripe",
            content=json.dumps(event).encode(),
            headers={"Stripe-Signature": "t=1,v1=test"},
        )

    assert first.status_code == 200, first.text
    assert first.json()["duplicate"] is False
    assert second.status_code == 200
    assert second.json()["duplicate"] is True

    status = client.get(
        "/api/v1/billing/status",
        headers={"Authorization": f"Bearer {token}"},
    )
    assert status.status_code == 200
    body = status.json()
    assert body["subscription_status"] == "active"
    assert body["stripe_subscription_id"] == "sub_wh_1"


def test_billing_summary_requires_admin(client: TestClient, stripe_env: None) -> None:
    token = _register_and_token(client, email="billing-summary-user@example.com")
    response = client.get(
        "/api/v1/billing/summary",
        headers={"Authorization": f"Bearer {token}"},
    )
    assert response.status_code == 403


def test_checkout_unconfigured_returns_400(client: TestClient) -> None:
    # Ensure Stripe keys are empty for this process settings cache.
    os.environ.pop("STRIPE_SECRET_KEY", None)
    os.environ.pop("STRIPE_PRICE_ID", None)
    get_settings.cache_clear()
    from app.billing.client import clear_stripe_client_cache

    clear_stripe_client_cache()

    token = _register_and_token(client, email="billing-unconfigured@example.com")
    response = client.post(
        "/api/v1/billing/checkout-session",
        headers={"Authorization": f"Bearer {token}"},
        json={},
    )
    assert response.status_code == 400
