"""Verify Microsoft Entra / MSAL ID tokens via JWKS."""

from __future__ import annotations

from dataclasses import dataclass
from functools import lru_cache
from typing import Any

import httpx
from jose import JWTError, jwt

from app.core.config import get_settings
from app.core.logging import get_logger, log_event

logger = get_logger(__name__)


@dataclass(frozen=True)
class VerifiedIdentity:
    """Provider-verified identity claims. Never log email/tokens."""

    subject: str
    email: str | None
    email_verified: bool
    full_name: str | None
    provider: str = "microsoft"


def _jwks_url(tenant_id: str) -> str:
    return f"https://login.microsoftonline.com/{tenant_id}/discovery/v2.0/keys"


@lru_cache(maxsize=8)
def _fetch_jwks(tenant_id: str) -> dict[str, Any]:
    url = _jwks_url(tenant_id)
    with httpx.Client(timeout=10.0) as client:
        response = client.get(url)
        response.raise_for_status()
        return response.json()


def clear_jwks_cache() -> None:
    """Test helper: drop cached JWKS documents."""
    _fetch_jwks.cache_clear()


def _rsa_key_for_token(token: str, jwks: dict[str, Any]) -> dict[str, Any]:
    headers = jwt.get_unverified_header(token)
    kid = headers.get("kid")
    if not kid:
        raise ValueError("Microsoft ID token missing kid")
    for key in jwks.get("keys", []):
        if key.get("kid") == kid:
            return key
    raise ValueError("No matching JWKS key for token")


def verify_microsoft_id_token(raw_token: str) -> VerifiedIdentity:
    """
    Validate a Microsoft ID token (audience = client ID) using tenant JWKS.

    Raises ValueError on misconfiguration or invalid tokens.
    Does not log the token or email (HIPAA / PII).
    """
    settings = get_settings()
    client_id = settings.microsoft_client_id.strip()
    tenant_id = (settings.microsoft_tenant_id or "common").strip()
    if not client_id:
        raise ValueError("Microsoft client ID is not configured")

    try:
        jwks = _fetch_jwks(tenant_id)
        rsa_key = _rsa_key_for_token(raw_token, jwks)
        claims = jwt.decode(
            raw_token,
            rsa_key,  # type: ignore[arg-type]
            algorithms=["RS256"],
            audience=client_id,
            options={"verify_at_hash": False},
        )
    except (JWTError, httpx.HTTPError, ValueError, KeyError, TypeError) as exc:
        log_event(logger, "microsoft_id_token_verify_failed", reason=type(exc).__name__)
        raise ValueError("Invalid Microsoft ID token") from exc

    issuer = str(claims.get("iss", ""))
    if "login.microsoftonline.com" not in issuer or "/v2.0" not in issuer:
        log_event(logger, "microsoft_id_token_bad_issuer")
        raise ValueError("Invalid Microsoft ID token issuer")

    subject = str(claims.get("sub") or claims.get("oid") or "").strip()
    if not subject:
        raise ValueError("Microsoft ID token missing subject")

    email = claims.get("email") or claims.get("preferred_username")
    full_name = claims.get("name")

    log_event(logger, "microsoft_id_token_verified", provider="microsoft")
    return VerifiedIdentity(
        subject=subject,
        email=str(email) if email else None,
        email_verified=True,
        full_name=str(full_name) if full_name else None,
        provider="microsoft",
    )
