"""Verify Google Identity Services (One Tap) ID tokens via google-auth."""

from __future__ import annotations

from dataclasses import dataclass

from google.auth.transport import requests as google_requests
from google.oauth2 import id_token

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
    provider: str = "google"


def verify_google_id_token(raw_token: str) -> VerifiedIdentity:
    """
    Validate a Google ID token against GOOGLE_CLIENT_ID.

    Raises ValueError on misconfiguration or invalid tokens.
    Does not log the token or email (HIPAA / PII).
    """
    settings = get_settings()
    audience = settings.google_client_id.strip()
    if not audience:
        raise ValueError("Google client ID is not configured")

    try:
        claims = id_token.verify_oauth2_token(
            raw_token,
            google_requests.Request(),
            audience,
        )
    except Exception as exc:  # google.auth.exceptions.GoogleAuthError and ValueError
        log_event(logger, "google_id_token_verify_failed", reason=type(exc).__name__)
        raise ValueError("Invalid Google ID token") from exc

    issuer = str(claims.get("iss", ""))
    if issuer not in {"accounts.google.com", "https://accounts.google.com"}:
        log_event(logger, "google_id_token_bad_issuer")
        raise ValueError("Invalid Google ID token issuer")

    subject = str(claims.get("sub") or "").strip()
    if not subject:
        raise ValueError("Google ID token missing subject")

    email = claims.get("email")
    email_verified = bool(claims.get("email_verified", False))
    full_name = claims.get("name")

    log_event(logger, "google_id_token_verified", provider="google")
    return VerifiedIdentity(
        subject=subject,
        email=str(email) if email else None,
        email_verified=email_verified,
        full_name=str(full_name) if full_name else None,
        provider="google",
    )
