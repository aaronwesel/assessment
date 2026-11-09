"""
Auth HTTP surface: password register/login, Google/Microsoft One Tap exchange,
JWT refresh/logout, and RBAC-protected introspection.

Providers return our own JWT pair (access + refresh). Federated ID tokens are
verified server-side then discarded — we never persist Google/Microsoft tokens.

HIPAA / SES:
- Audit logs use event codes only (no email/password/id_token in logs).
- Optional login alerts via SES are ops-mailbox only and omit account identifiers.
"""

from __future__ import annotations

from typing import Annotated, Literal

from fastapi import APIRouter, Depends, HTTPException, status
from pydantic import BaseModel, EmailStr, Field

from app.alerts.login_alert import maybe_send_login_alert
from app.api.deps import RedisDep, SessionDep
from app.core.config import get_settings
from app.core.logging import get_logger, log_event
from app.core.rbac import AuthContext, Role, get_current_auth, require_role
from app.core.security import (
    create_access_token,
    create_refresh_token,
    decode_token,
    revoke_token,
)
from app.models.account_link import IdentityProvider
from app.services import users as user_service
from app.services.google_id_token import verify_google_id_token
from app.services.microsoft_id_token import verify_microsoft_id_token

router = APIRouter(prefix="/auth", tags=["auth"])
logger = get_logger(__name__)


class TokenPair(BaseModel):
    access_token: str
    refresh_token: str
    token_type: str = "bearer"


class PasswordRegisterRequest(BaseModel):
    email: EmailStr
    password: str = Field(min_length=8, max_length=128)
    full_name: str | None = Field(default=None, max_length=255)


class PasswordLoginRequest(BaseModel):
    email: EmailStr
    password: str = Field(min_length=1, max_length=128)


class RefreshRequest(BaseModel):
    refresh_token: str


class LogoutRequest(BaseModel):
    access_token: str | None = None
    refresh_token: str | None = None


class OneTapExchangeRequest(BaseModel):
    """ID token from Google Identity Services or Microsoft MSAL One Tap."""

    id_token: str = Field(min_length=10)
    provider: Literal["google", "microsoft"]


class ProtectedPingResponse(BaseModel):
    subject: str
    roles: list[str]
    email: str | None = None
    full_name: str | None = None
    message: str = "ok"


class AuthProvidersResponse(BaseModel):
    google: bool
    microsoft: bool
    password: bool = True
    ses_enabled: bool
    login_alert_email_enabled: bool


def _issue_tokens(subject: str, roles: list[str]) -> TokenPair:
    """Mint access+refresh JWTs and register both jtis in Redis sessions."""
    return TokenPair(
        access_token=create_access_token(subject=subject, roles=roles),
        refresh_token=create_refresh_token(subject=subject),
    )


@router.get("/providers", response_model=AuthProvidersResponse)
def auth_providers() -> AuthProvidersResponse:
    """Public capability probe for the SPA (which buttons to show)."""
    settings = get_settings()
    return AuthProvidersResponse(
        google=settings.google_configured,
        microsoft=settings.microsoft_configured,
        password=True,
        ses_enabled=settings.ses_enabled and bool(settings.ses_from_email),
        login_alert_email_enabled=settings.login_alert_email_enabled,
    )


@router.post("/register", response_model=TokenPair, status_code=status.HTTP_201_CREATED)
def register_password(body: PasswordRegisterRequest, session: SessionDep) -> TokenPair:
    # HIPAA: do not log email/password.
    log_event(logger, "password_register")
    try:
        user = user_service.create_password_user(
            session,
            email=body.email,
            password=body.password,
            full_name=body.full_name,
        )
    except ValueError as exc:
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail=str(exc)) from exc
    return _issue_tokens(subject=user.id, roles=user_service.role_values(user))


@router.post("/login", response_model=TokenPair)
def login_password(body: PasswordLoginRequest, session: SessionDep) -> TokenPair:
    log_event(logger, "password_login")
    user = user_service.authenticate_password(
        session,
        email=body.email,
        password=body.password,
    )
    if user is None:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid credentials",
        )
    # Optional SES login alert (LOGIN_ALERT_EMAIL_ENABLED=false by default).
    # Does not embed email/subject in the message body (HIPAA: no PHI in alerts).
    maybe_send_login_alert(event_code="password_login_success")
    return _issue_tokens(subject=user.id, roles=user_service.role_values(user))


@router.post("/refresh", response_model=TokenPair)
def refresh_tokens(body: RefreshRequest, session: SessionDep) -> TokenPair:
    """Rotate refresh: blacklist old jti, issue a fresh access+refresh pair."""
    try:
        payload = decode_token(body.refresh_token, expected_type="refresh")
    except ValueError as exc:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid refresh token",
        ) from exc
    subject = str(payload.get("sub", ""))
    if not subject:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Invalid token")

    user = user_service.get_user_by_id(session, subject)
    if user is None or not user.is_active:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="User unavailable")

    # Rotate: revoke old refresh, issue new pair.
    revoke_token(body.refresh_token)
    return _issue_tokens(subject=user.id, roles=user_service.role_values(user))


@router.post("/logout", status_code=status.HTTP_204_NO_CONTENT)
def logout(body: LogoutRequest, _redis: RedisDep) -> None:
    """Best-effort revoke: blacklist provided tokens in Redis until expiry."""
    log_event(logger, "logout")
    if body.access_token:
        revoke_token(body.access_token)
    if body.refresh_token:
        revoke_token(body.refresh_token)


@router.post("/google/onetap", response_model=TokenPair)
def google_onetap_exchange(body: OneTapExchangeRequest, session: SessionDep) -> TokenPair:
    """Exchange a Google One Tap ID token for our JWT pair."""
    if body.provider != "google":
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="provider must be google")
    settings = get_settings()
    if not settings.google_configured:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Google sign-in is not configured",
        )
    log_event(logger, "google_onetap_exchange")
    try:
        identity = verify_google_id_token(body.id_token)
        user = user_service.upsert_federated_user(
            session,
            provider=IdentityProvider.GOOGLE,
            provider_subject=identity.subject,
            email=identity.email,
            full_name=identity.full_name,
        )
    except ValueError as exc:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid Google credentials",
        ) from exc
    maybe_send_login_alert(event_code="google_onetap_login_success")
    return _issue_tokens(subject=user.id, roles=user_service.role_values(user))


@router.post("/microsoft/onetap", response_model=TokenPair)
def microsoft_onetap_exchange(body: OneTapExchangeRequest, session: SessionDep) -> TokenPair:
    """Exchange a Microsoft MSAL / One Tap ID token for our JWT pair."""
    if body.provider != "microsoft":
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="provider must be microsoft",
        )
    settings = get_settings()
    if not settings.microsoft_configured:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Microsoft sign-in is not configured",
        )
    log_event(logger, "microsoft_onetap_exchange")
    try:
        identity = verify_microsoft_id_token(body.id_token)
        user = user_service.upsert_federated_user(
            session,
            provider=IdentityProvider.MICROSOFT,
            provider_subject=identity.subject,
            email=identity.email,
            full_name=identity.full_name,
        )
    except ValueError as exc:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid Microsoft credentials",
        ) from exc
    maybe_send_login_alert(event_code="microsoft_onetap_login_success")
    return _issue_tokens(subject=user.id, roles=user_service.role_values(user))


@router.get("/me", response_model=ProtectedPingResponse)
def auth_me(
    auth: Annotated[AuthContext, Depends(get_current_auth)],
    session: SessionDep,
) -> ProtectedPingResponse:
    """Return the authenticated profile for the SPA home screen."""
    user = user_service.get_user_by_id(session, auth.subject)
    return ProtectedPingResponse(
        subject=auth.subject,
        roles=sorted(auth.roles),
        email=user.email if user else None,
        full_name=user.full_name if user else None,
    )


@router.get(
    "/admin/ping",
    response_model=ProtectedPingResponse,
    dependencies=[Depends(require_role(Role.ADMIN))],
)
def admin_ping(
    auth: Annotated[AuthContext, Depends(get_current_auth)],
) -> ProtectedPingResponse:
    """RBAC smoke endpoint — requires admin role claim on the access token."""
    return ProtectedPingResponse(
        subject=auth.subject,
        roles=sorted(auth.roles),
        message="admin_ok",
    )
