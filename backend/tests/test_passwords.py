"""Password policy + login lockout stubs."""

from __future__ import annotations

import pytest
from fastapi.testclient import TestClient

from app.core.passwords import PasswordPolicyError, validate_password_complexity
from app.core.rate_limit import backoff_seconds_for_failures


def test_password_complexity_rejects_short_and_common() -> None:
    with pytest.raises(PasswordPolicyError):
        validate_password_complexity("Short1!")
    with pytest.raises(PasswordPolicyError):
        validate_password_complexity("password123!")
    with pytest.raises(PasswordPolicyError):
        validate_password_complexity("alllowercase1!")
    validate_password_complexity("SecurePass1!x")


def test_register_rejects_weak_password(client: TestClient) -> None:
    # Long enough for the request schema, but fails complexity / denylist.
    response = client.post(
        "/api/v1/auth/register",
        json={"email": "weak@example.com", "password": "password1234"},
    )
    assert response.status_code == 400


def test_login_lockout_after_failures(client: TestClient, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("LOGIN_MAX_FAILURES", "3")
    monkeypatch.setenv("LOGIN_LOCKOUT_SECONDS", "600")
    from app.core.config import get_settings

    get_settings.cache_clear()

    client.post(
        "/api/v1/auth/register",
        json={"email": "lock@example.com", "password": "SecurePass1!x"},
    )
    for _ in range(3):
        bad = client.post(
            "/api/v1/auth/login",
            json={"email": "lock@example.com", "password": "WrongPass1!x"},
        )
        assert bad.status_code == 401

    locked = client.post(
        "/api/v1/auth/login",
        json={"email": "lock@example.com", "password": "SecurePass1!x"},
    )
    assert locked.status_code == 429
    get_settings.cache_clear()


def test_backoff_stub_schedule() -> None:
    assert backoff_seconds_for_failures(1) == 0
    assert backoff_seconds_for_failures(3) == 30
    assert backoff_seconds_for_failures(99) >= 30
