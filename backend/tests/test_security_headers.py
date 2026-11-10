"""Security headers middleware baseline."""

from fastapi.testclient import TestClient


def test_security_headers_present(client: TestClient) -> None:
    response = client.get("/health")
    assert response.status_code == 200
    assert response.headers.get("X-Content-Type-Options") == "nosniff"
    assert response.headers.get("X-Frame-Options") == "DENY"
    assert "default-src" in (response.headers.get("Content-Security-Policy") or "")
    # HSTS skipped in test environment.
    assert response.headers.get("Strict-Transport-Security") is None
