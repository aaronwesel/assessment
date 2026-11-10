"""Redis feature flags / kill switches."""

from __future__ import annotations

from fastapi.testclient import TestClient

from app.core.redis import get_redis, reset_redis_for_tests
from app.flags.service import get_flag, set_flag
from tests.helpers_auth import admin_auth_header, register_user


def test_flag_kill_switch_roundtrip(client: TestClient) -> None:
    reset_redis_for_tests()
    headers = admin_auth_header(client, email="flags-admin@example.com")

    listed = client.get("/api/v1/flags", headers=headers)
    assert listed.status_code == 200
    names = {item["name"] for item in listed.json()}
    assert "ai_deepseek" in names
    assert "slack" in names

    disabled = client.put(
        "/api/v1/flags/ai_deepseek",
        headers=headers,
        json={"enabled": False},
    )
    assert disabled.status_code == 200
    assert disabled.json()["enabled"] is False
    assert get_flag("ai_deepseek") is False

    cleared = client.delete("/api/v1/flags/ai_deepseek", headers=headers)
    assert cleared.status_code == 200
    # Default from settings (DEEPSEEK_ENABLED=false in tests).
    assert cleared.json()["enabled"] is False


def test_flag_denies_non_admin(client: TestClient) -> None:
    user = register_user(client, email="flags-user@example.com")
    response = client.get(
        "/api/v1/flags",
        headers={"Authorization": f"Bearer {user['access_token']}"},
    )
    assert response.status_code == 403


def test_set_flag_service_direct() -> None:
    reset_redis_for_tests()
    redis = get_redis()
    set_flag("ses", True, redis=redis)
    assert get_flag("ses", redis=redis) is True
    set_flag("ses", False, redis=redis)
    assert get_flag("ses", redis=redis) is False
