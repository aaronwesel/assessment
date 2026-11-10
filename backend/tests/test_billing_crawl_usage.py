"""Crawl pages-with-results ledger: 5000 free tier + Stripe meter hooks."""

from __future__ import annotations

from unittest.mock import patch

import pytest
from fastapi.testclient import TestClient
from sqlmodel import Session

from app.billing.client import clear_stripe_client_cache
from app.billing.models import BillingCustomer
from app.core.config import get_settings
from app.db.session import engine
from tests.helpers_auth import register_user


def _auth_header(client: TestClient, email: str = "crawl-usage@example.com") -> dict[str, str]:
    user = register_user(client, email=email)
    return {"Authorization": f"Bearer {user['access_token']}"}


@pytest.fixture
def stripe_meter_env(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("STRIPE_SECRET_KEY", "sk_test_dummy_not_real")
    monkeypatch.setenv("STRIPE_PUBLISHABLE_KEY", "pk_test_dummy_not_real")
    monkeypatch.setenv("STRIPE_WEBHOOK_SECRET", "whsec_test_dummy")
    monkeypatch.setenv("STRIPE_PRICE_ID", "price_test_dummy")
    monkeypatch.setenv("STRIPE_METERED_PRICE_ID", "price_meter_test")
    monkeypatch.setenv("STRIPE_METER_EVENT_NAME", "crawl_pages_with_results")
    monkeypatch.setenv("CRAWL_FREE_PAGES", "5000")
    get_settings.cache_clear()
    clear_stripe_client_cache()
    yield
    get_settings.cache_clear()
    clear_stripe_client_cache()


def test_crawl_usage_requires_auth(client: TestClient) -> None:
    res = client.get("/api/v1/billing/crawl-usage")
    assert res.status_code == 401


def test_growth_pricing_includes_free_tier(client: TestClient, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("CRAWL_FREE_PAGES", "5000")
    get_settings.cache_clear()
    res = client.get("/api/v1/growth/crawl-pricing")
    assert res.status_code == 200
    body = res.json()
    assert body["free_pages_allowance"] == 5000
    assert body["billable_unit"] == "pages_with_results"
    get_settings.cache_clear()


def test_record_and_free_tier(client: TestClient) -> None:
    headers = _auth_header(client, "free-tier@example.com")
    status = client.get("/api/v1/billing/crawl-usage", headers=headers)
    assert status.status_code == 200
    body = status.json()
    assert body["free_pages_allowance"] == 5000
    assert body["pages_with_results_total"] == 0
    assert body["free_pages_remaining"] == 5000

    record = client.post(
        "/api/v1/billing/crawl-usage/record",
        headers=headers,
        json={"crawl_job_id": "11111111-1111-1111-1111-111111111111", "pages_with_results": 12},
    )
    assert record.status_code == 200
    rec = record.json()
    assert rec["recorded"] is True
    assert rec["duplicate"] is False
    assert rec["billable_pages"] == 0
    assert rec["pages_with_results_total"] == 12
    assert rec["free_pages_remaining"] == 4988

    dup = client.post(
        "/api/v1/billing/crawl-usage/record",
        headers=headers,
        json={"crawl_job_id": "11111111-1111-1111-1111-111111111111", "pages_with_results": 12},
    )
    assert dup.status_code == 200
    assert dup.json()["duplicate"] is True


def test_billable_pages_after_free_tier(client: TestClient, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("CRAWL_FREE_PAGES", "10")
    get_settings.cache_clear()
    headers = _auth_header(client, "billable@example.com")
    record = client.post(
        "/api/v1/billing/crawl-usage/record",
        headers=headers,
        json={"crawl_job_id": "22222222-2222-2222-2222-222222222222", "pages_with_results": 15},
    )
    get_settings.cache_clear()
    assert record.status_code == 200
    rec = record.json()
    assert rec["billable_pages"] == 5
    assert rec["free_pages_remaining"] == 0


def test_meter_event_when_customer_exists(
    client: TestClient,
    stripe_meter_env: None,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setenv("CRAWL_FREE_PAGES", "0")
    get_settings.cache_clear()
    user = register_user(client, email="meter@example.com")

    with Session(engine) as session:
        session.add(
            BillingCustomer(
                user_id=user["user_id"],
                stripe_customer_id="cus_meter_test",
                email="meter@example.com",
            )
        )
        session.commit()

    headers = {"Authorization": f"Bearer {user['access_token']}"}
    with patch(
        "app.billing.crawl_usage.stripe_client.create_meter_event",
        return_value={"object": "billing.meter_event"},
    ) as meter_mock:
        record = client.post(
            "/api/v1/billing/crawl-usage/record",
            headers=headers,
            json={"crawl_job_id": "33333333-3333-3333-3333-333333333333", "pages_with_results": 3},
        )
    assert record.status_code == 200
    rec = record.json()
    assert rec["billable_pages"] == 3
    assert rec["stripe_meter_events_sent"] == 3
    meter_mock.assert_called_once()
