"""Unit tests for Google / Microsoft ID token verification (mocked crypto/JWKS)."""

from __future__ import annotations

from unittest.mock import MagicMock, patch

import pytest

from app.services import google_id_token as google
from app.services import microsoft_id_token as microsoft


def test_google_verify_success() -> None:
    claims = {
        "iss": "https://accounts.google.com",
        "sub": "g-sub",
        "email": "g@example.com",
        "email_verified": True,
        "name": "G User",
    }
    with patch.object(google.id_token, "verify_oauth2_token", return_value=claims) as mocked:
        identity = google.verify_google_id_token("raw-id-token")
        mocked.assert_called_once()
    assert identity.subject == "g-sub"
    assert identity.email == "g@example.com"
    assert identity.provider == "google"


def test_google_verify_rejects_bad_issuer() -> None:
    claims = {"iss": "https://evil.example", "sub": "x"}
    with patch.object(google.id_token, "verify_oauth2_token", return_value=claims):
        with pytest.raises(ValueError, match="issuer"):
            google.verify_google_id_token("raw-id-token")


def test_google_requires_client_id() -> None:
    with patch("app.services.google_id_token.get_settings") as gs:
        settings = MagicMock()
        settings.google_client_id = ""
        gs.return_value = settings
        with pytest.raises(ValueError, match="not configured"):
            google.verify_google_id_token("raw-id-token")


def test_microsoft_requires_client_id() -> None:
    with patch("app.services.microsoft_id_token.get_settings") as gs:
        settings = MagicMock()
        settings.microsoft_client_id = ""
        settings.microsoft_tenant_id = "common"
        gs.return_value = settings
        with pytest.raises(ValueError, match="not configured"):
            microsoft.verify_microsoft_id_token("raw-id-token")


def test_microsoft_jwks_kid_mismatch(monkeypatch: pytest.MonkeyPatch) -> None:
    microsoft.clear_jwks_cache()

    class FakeResponse:
        def raise_for_status(self) -> None:
            return None

        def json(self) -> dict:
            return {"keys": [{"kid": "other", "kty": "RSA"}]}

    class FakeClient:
        def __init__(self, *args, **kwargs) -> None:
            pass

        def __enter__(self):
            return self

        def __exit__(self, *args):
            return False

        def get(self, url: str):
            return FakeResponse()

    monkeypatch.setattr(microsoft.httpx, "Client", FakeClient)
    monkeypatch.setattr(
        microsoft.jwt,
        "get_unverified_header",
        lambda _t: {"kid": "missing"},
    )
    with pytest.raises(ValueError, match="Invalid Microsoft"):
        microsoft.verify_microsoft_id_token("a.b.c")
    microsoft.clear_jwks_cache()
