"""Extended health / ready / SLO metrics for platform ops."""

from __future__ import annotations

from fastapi.testclient import TestClient

from app.health.metrics import record_request, reset_metrics_for_tests


def test_ready_alias(client: TestClient) -> None:
    response = client.get("/ready")
    assert response.status_code == 200
    body = response.json()
    assert body["status"] == "ready"
    assert "feature_flags" in body
    assert "webhooks_enabled" in body


def test_health_metrics_and_slo(client: TestClient) -> None:
    reset_metrics_for_tests()
    record_request(route="/health", status_code=200, latency_ms=12.5)
    record_request(route="/api/v1/audit/events", status_code=500, latency_ms=40.0)

    metrics = client.get("/health/metrics")
    assert metrics.status_code == 200
    payload = metrics.json()["metrics"]
    assert payload["requests_total"] == 2
    assert payload["errors_5xx"] == 1
    assert payload["slo"]["availability_target"] == 0.99

    slo = client.get("/health/slo")
    assert slo.status_code == 200
    assert slo.json()["requests_total"] == 2

    # Also available under versioned prefix.
    assert client.get("/api/v1/health/metrics").status_code == 200


def test_billing_and_growth_mounts(client: TestClient) -> None:
    # Billing (workstream B) requires auth; unauthenticated callers get 401.
    billing = client.get("/api/v1/billing/status")
    assert billing.status_code == 401

    growth = client.get("/api/v1/growth/status")
    assert growth.status_code == 200
    assert growth.json()["workstream"] == "C"
