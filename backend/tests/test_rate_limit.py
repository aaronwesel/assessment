"""Optional Redis rate-limit middleware tests."""

from __future__ import annotations

import os

from fastapi.testclient import TestClient


def test_rate_limit_disabled_by_default(client: TestClient) -> None:
    for _ in range(5):
        assert client.get("/api/v1/health").status_code == 200


def test_rate_limit_when_enabled(monkeypatch) -> None:
    monkeypatch.setenv("RATE_LIMIT_ENABLED", "true")
    monkeypatch.setenv("RATE_LIMIT_REQUESTS", "2")
    monkeypatch.setenv("RATE_LIMIT_WINDOW_SECONDS", "60")

    from app.core.config import get_settings
    from app.core.redis import get_redis
    from app.main import create_app

    get_settings.cache_clear()
    # Reset redis singleton so incr uses a fresh stub store.
    import app.core.redis as redis_mod

    redis_mod._redis_stub = None
    get_redis()

    application = create_app()
    with TestClient(application) as test_client:
        assert test_client.get("/api/v1/auth/providers").status_code == 200
        assert test_client.get("/api/v1/auth/providers").status_code == 200
        limited = test_client.get("/api/v1/auth/providers")
        assert limited.status_code == 429
        # Health stays exempt for ALB probes.
        assert test_client.get("/health").status_code == 200

    get_settings.cache_clear()
    os.environ.pop("RATE_LIMIT_ENABLED", None)
    os.environ.pop("RATE_LIMIT_REQUESTS", None)
    os.environ.pop("RATE_LIMIT_WINDOW_SECONDS", None)
    redis_mod._redis_stub = None
