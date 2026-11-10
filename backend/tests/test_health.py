"""Smoke tests for health endpoints."""

from fastapi.testclient import TestClient


def test_health_ok(client: TestClient) -> None:
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json() == {"status": "ok"}


def test_health_ready(client: TestClient) -> None:
    response = client.get("/health/ready")
    assert response.status_code == 200
    body = response.json()
    assert body["status"] == "ready"
    assert body["db_ok"] is True
    assert body["redis_ok"] is True
    assert body["deepseek_enabled"] is False
    assert body["rekognition_enabled"] is False
    assert body["ses_enabled"] is False
    assert "feature_flags" in body
    assert body["webhooks_enabled"] is True


def test_api_v1_health(client: TestClient) -> None:
    response = client.get("/api/v1/health")
    assert response.status_code == 200
    assert response.json()["status"] == "ok"
