"""Request-ID middleware smoke tests."""

from fastapi.testclient import TestClient


def test_generates_request_id(client: TestClient) -> None:
    response = client.get("/health")
    assert response.status_code == 200
    assert "X-Request-ID" in response.headers
    assert len(response.headers["X-Request-ID"]) >= 8


def test_echoes_client_request_id(client: TestClient) -> None:
    response = client.get("/health", headers={"X-Request-ID": "client-trace-42"})
    assert response.status_code == 200
    assert response.headers["X-Request-ID"] == "client-trace-42"
