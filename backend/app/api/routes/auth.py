"""
Auth HTTP surface: password register/login, Google/Microsoft One Tap exchange,
JWT refresh/logout, MFA (TOTP), and RBAC-protected introspection.

Providers return our own JWT pair (access + refresh). Federated ID tokens are
verified server-side then discarded — we never persist Google/Microsoft tokens.

HIPAA / security:
- Audit logs use event codes only (no email/password/id_token/TOTP/backup codes).
- Login is rate-limited / lockout-stubbed via Redis (opaque email hashes).
- Password complexity enforced at register; MFA secrets Fernet-encrypted.
- Refresh rotates + requires live Redis session entry.
- Optional login alerts via SES are ops-mailbox only and omit account identifiers.
"""

from __future__ import annotations

from typing import Annotated, Literal

from fastapi import APIRouter, Depends, HTTPException, status
from pydantic import BaseModel, EmailStr, Field

from app.alerts.login_alert import maybe_send_login_alert
from app.api.deps import CurrentAuth, RedisDep, SessionDep
from app.core.config import get_settings
from app.core.logging import get_logger, log_event
from app.core.passwords import MIN_PASSWORD_LENGTH, MAX_PASSWORD_LENGTH
from app.core.rate_limit import (
    clear_login_failures,
    is_login_locked,
    record_login_failure,
)
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
    password: str = Field(min_length=MIN_PASSWORD_LENGTH, max_length=MAX_PASSWORD_LENGTH)
    full_name: str | None = Field(default=None, max_length=255)
    # Healthcare / HIPAA-minded orgs must complete MFA enrollment (policy flag only).
    healthcare_account: bool = False


class PasswordLoginRequest(BaseModel):
    email: EmailStr
    password: str = Field(min_length=1, max_length=MAX_PASSWORD_LENGTH)
    # Required when the account has mfa_enabled; optional otherwise.
    totp_code: str | None = Field(default=None, max_length=16)
    backup_code: str | None = Field(default=None, max_length=64)


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
    mfa_enabled: bool = False
    mfa_policy_required: bool = False
    message: str = "ok"


class AuthProvidersResponse(BaseModel):
    google: bool
    microsoft: bool
    password: bool = True
    ses_enabled: bool
    login_alert_email_enabled: bool
    mfa_available: bool = True


class MfaEnrollResponse(BaseModel):
    """Provisioning material — treat as secret; frontend must not log it."""

    secret: str
    otpauth_uri: str


class MfaConfirmRequest(BaseModel):
    totp_code: str = Field(min_length=6, max_length=16)


class MfaConfirmResponse(BaseModel):
    mfa_enabled: bool = True
    backup_codes: list[str]


class MfaVerifyRequest(BaseModel):
    totp_code: str | None = Field(default=None, max_length=16)
    backup_code: str | None = Field(default=None, max_length=64)


class MfaVerifyResponse(BaseModel):
    ok: bool


class MfaStatusResponse(BaseModel):
    mfa_enabled: bool
    mfa_policy_required: bool = False


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
        mfa_available=True,
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
            mfa_policy_required=body.healthcare_account,
        )
    except ValueError as exc:
        detail = str(exc)
        code = (
            status.HTTP_409_CONFLICT
            if detail == "User already exists"
            else status.HTTP_400_BAD_REQUEST
        )
        raise HTTPException(status_code=code, detail=detail) from exc
    return _issue_tokens(subject=user.id, roles=user_service.role_values(user))


@router.post("/login", response_model=TokenPair)
def login_password(
    body: PasswordLoginRequest,
    session: SessionDep,
    redis: RedisDep,
) -> TokenPair:
    log_event(logger, "password_login")

    if is_login_locked(body.email, redis):
        # Do not reveal whether the account exists.
        raise HTTPException(
            status_code=status.HTTP_429_TOO_MANY_REQUESTS,
            detail="Too many failed attempts; try again later",
        )

    user = user_service.authenticate_password(
        session,
        email=body.email,
        password=body.password,
    )
    if user is None:
        record_login_failure(body.email, redis)
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid credentials",
        )

    # MFA gate: password alone is insufficient when the user has enrolled TOTP.
    if user.mfa_enabled:
        if not body.totp_code and not body.backup_code:
            raise HTTPException(
                status_code=status.HTTP_403_FORBIDDEN,
                detail="mfa_required",
            )
        if not user_service.verify_user_mfa(
            session,
            user,
            totp_code=body.totp_code,
            backup_code=body.backup_code,
        ):
            record_login_failure(body.email, redis)
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Invalid MFA code",
            )

    clear_login_failures(body.email, redis)
    # Optional SES login alert (LOGIN_ALERT_EMAIL_ENABLED=false by default).
    # Does not embed email/subject in the message body (HIPAA: no PHI in alerts).
    maybe_send_login_alert(event_code="password_login_success")
    return _issue_tokens(subject=user.id, roles=user_service.role_values(user))


@router.post("/refresh", response_model=TokenPair)
def refresh_tokens(body: RefreshRequest, session: SessionDep) -> TokenPair:
    """Rotate refresh: blacklist old jti, issue a fresh access+refresh pair."""
    try:
        # require_session=True: Redis session registry must still hold this refresh jti.
        payload = decode_token(
            body.refresh_token,
            expected_type="refresh",
            require_session=True,
        )
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


# --- MFA endpoints -----------------------------------------------------------------


@router.get("/mfa/status", response_model=MfaStatusResponse)
def mfa_status(
    auth: CurrentAuth,
    session: SessionDep,
) -> MfaStatusResponse:
    user = user_service.get_user_by_id(session, auth.subject)
    if user is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="User not found")
    return MfaStatusResponse(
        mfa_enabled=bool(user.mfa_enabled),
        mfa_policy_required=bool(user.mfa_policy_required),
    )


@router.post("/mfa/enroll", response_model=MfaEnrollResponse)
def mfa_enroll(
    auth: CurrentAuth,
    session: SessionDep,
) -> MfaEnrollResponse:
    """Start TOTP enrollment: returns secret + otpauth URI (do not log)."""
    user = user_service.get_user_by_id(session, auth.subject)
    if user is None or not user.is_active:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="User not found")
    if user.mfa_enabled:
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT,
            detail="MFA already enabled",
        )
    settings = get_settings()
    material = user_service.begin_mfa_enrollment(
        session,
        user,
        issuer=settings.mfa_issuer,
    )
    log_event(logger, "mfa_enroll_api")
    return MfaEnrollResponse(secret=material["secret"], otpauth_uri=material["otpauth_uri"])


@router.post("/mfa/confirm", response_model=MfaConfirmResponse)
def mfa_confirm(
    body: MfaConfirmRequest,
    auth: CurrentAuth,
    session: SessionDep,
) -> MfaConfirmResponse:
    """Confirm enrollment with a live TOTP code; returns one-time backup codes."""
    user = user_service.get_user_by_id(session, auth.subject)
    if user is None or not user.is_active:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="User not found")
    try:
        backup_codes = user_service.confirm_mfa_enrollment(
            session,
            user,
            totp_code=body.totp_code,
        )
    except ValueError as exc:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(exc)) from exc
    log_event(logger, "mfa_confirm_api")
    return MfaConfirmResponse(backup_codes=backup_codes)


@router.post("/mfa/verify", response_model=MfaVerifyResponse)
def mfa_verify(
    body: MfaVerifyRequest,
    auth: CurrentAuth,
    session: SessionDep,
) -> MfaVerifyResponse:
    """Verify TOTP or backup code for an already-authenticated user (UI stub support)."""
    user = user_service.get_user_by_id(session, auth.subject)
    if user is None or not user.is_active:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="User not found")
    if not user.mfa_enabled:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="MFA not enabled")
    ok = user_service.verify_user_mfa(
        session,
        user,
        totp_code=body.totp_code,
        backup_code=body.backup_code,
    )
    if not ok:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Invalid MFA code")
    return MfaVerifyResponse(ok=True)


@router.post("/mfa/disable", status_code=status.HTTP_204_NO_CONTENT)
def mfa_disable(
    body: MfaVerifyRequest,
    auth: CurrentAuth,
    session: SessionDep,
) -> None:
    """Disable MFA after proving possession of TOTP or a backup code."""
    user = user_service.get_user_by_id(session, auth.subject)
    if user is None or not user.is_active:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="User not found")
    if not user.mfa_enabled:
        return
    ok = user_service.verify_user_mfa(
        session,
        user,
        totp_code=body.totp_code,
        backup_code=body.backup_code,
    )
    if not ok:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Invalid MFA code")
    user_service.disable_mfa(session, user)


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
        mfa_enabled=bool(user.mfa_enabled) if user else False,
        mfa_policy_required=bool(user.mfa_policy_required) if user else False,
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
