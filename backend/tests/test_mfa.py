"""TOTP MFA tests with a fixed base32 secret (deterministic codes)."""

from __future__ import annotations

import pyotp
from fastapi.testclient import TestClient

from app.core.encryption import encrypt_field
from app.core.mfa import verify_totp
from app.db.session import engine
from app.models.user import User
from sqlmodel import Session, select

# Classic pyotp example secret — fixed so tests do not depend on wall clock drift beyond the window.
FIXED_TOTP_SECRET = "JBSWY3DPEHPK3PXP"
STRONG_PASSWORD = "SecurePass1!x"


def _register(client: TestClient, email: str) -> dict:
    response = client.post(
        "/api/v1/auth/register",
        json={"email": email, "password": STRONG_PASSWORD, "full_name": "MFA User"},
    )
    assert response.status_code == 201, response.text
    return response.json()


def test_verify_totp_with_fixed_secret() -> None:
    """Unit: pyotp + verify_totp against the fixed base32 secret (current timestep)."""
    totp = pyotp.TOTP(FIXED_TOTP_SECRET)
    fixed_ts = 1_700_000_000
    code_at = totp.at(fixed_ts)
    assert len(code_at) == 6
    assert totp.verify(code_at, for_time=fixed_ts, valid_window=0)
    # Live window check uses the same fixed secret (not a random enroll secret).
    assert verify_totp(FIXED_TOTP_SECRET, totp.now())


def test_mfa_enroll_confirm_and_login_gate(client: TestClient) -> None:
    tokens = _register(client, "mfa-user@example.com")
    headers = {"Authorization": f"Bearer {tokens['access_token']}"}

    enroll = client.post("/api/v1/auth/mfa/enroll", headers=headers)
    assert enroll.status_code == 200
    body = enroll.json()
    assert "secret" in body
    assert body["otpauth_uri"].startswith("otpauth://totp/")

    # Replace the random secret with the fixed test secret so the TOTP is deterministic.
    with Session(engine) as session:
        user = session.exec(select(User).where(User.email == "mfa-user@example.com")).one()
        user.mfa_secret_encrypted = encrypt_field(FIXED_TOTP_SECRET)
        session.add(user)
        session.commit()

    code = pyotp.TOTP(FIXED_TOTP_SECRET).now()
    confirm = client.post(
        "/api/v1/auth/mfa/confirm",
        headers=headers,
        json={"totp_code": code},
    )
    assert confirm.status_code == 200, confirm.text
    confirmed = confirm.json()
    assert confirmed["mfa_enabled"] is True
    assert len(confirmed["backup_codes"]) >= 1

    # Password alone must fail with mfa_required.
    blocked = client.post(
        "/api/v1/auth/login",
        json={"email": "mfa-user@example.com", "password": STRONG_PASSWORD},
    )
    assert blocked.status_code == 403
    assert blocked.json()["detail"] == "mfa_required"

    login = client.post(
        "/api/v1/auth/login",
        json={
            "email": "mfa-user@example.com",
            "password": STRONG_PASSWORD,
            "totp_code": pyotp.TOTP(FIXED_TOTP_SECRET).now(),
        },
    )
    assert login.status_code == 200
    assert "access_token" in login.json()


def test_mfa_backup_code_login(client: TestClient) -> None:
    tokens = _register(client, "mfa-backup@example.com")
    headers = {"Authorization": f"Bearer {tokens['access_token']}"}
    client.post("/api/v1/auth/mfa/enroll", headers=headers)

    with Session(engine) as session:
        user = session.exec(select(User).where(User.email == "mfa-backup@example.com")).one()
        user.mfa_secret_encrypted = encrypt_field(FIXED_TOTP_SECRET)
        session.add(user)
        session.commit()

    confirm = client.post(
        "/api/v1/auth/mfa/confirm",
        headers=headers,
        json={"totp_code": pyotp.TOTP(FIXED_TOTP_SECRET).now()},
    )
    assert confirm.status_code == 200
    backup = confirm.json()["backup_codes"][0]

    login = client.post(
        "/api/v1/auth/login",
        json={
            "email": "mfa-backup@example.com",
            "password": STRONG_PASSWORD,
            "backup_code": backup,
        },
    )
    assert login.status_code == 200

    # Backup codes are single-use.
    reuse = client.post(
        "/api/v1/auth/login",
        json={
            "email": "mfa-backup@example.com",
            "password": STRONG_PASSWORD,
            "backup_code": backup,
        },
    )
    assert reuse.status_code == 401
