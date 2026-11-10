"""Auth stubs: password placeholders + Google/Microsoft One Tap token exchange."""

from __future__ import annotations

from typing import Annotated, Literal

from fastapi import APIRouter, Depends, HTTPException, status
from pydantic import BaseModel, EmailStr, Field

from app.core.logging import get_logger, log_event
from app.core.rbac import AuthContext, Role, get_current_auth, require_role
from app.core.security import (
    create_access_token,
    create_refresh_token,
    decode_token,
    hash_password,
    verify_password,
)

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


class OneTapExchangeRequest(BaseModel):
    """ID token from Google Identity Services or Microsoft MSAL One Tap."""

    id_token: str = Field(min_length=10)
    provider: Literal["google", "microsoft"]


class ProtectedPingResponse(BaseModel):
    subject: str
    roles: list[str]
    message: str = "ok"


# In-memory stub store for password flows until DB wiring is complete.
_STUB_USERS: dict[str, dict[str, str]] = {}


def _issue_tokens(subject: str, roles: list[str]) -> TokenPair:
    return TokenPair(
        access_token=create_access_token(subject=subject, roles=roles),
        refresh_token=create_refresh_token(subject=subject),
    )


def _stub_verify_id_token(id_token: str, *, provider: str) -> str:
    """
    Placeholder token verification.
    Real GIS / MSAL JWKS validation lands when OAuth client IDs are configured.
    Does not log the id_token (contains PII).
    """
    if len(id_token) < 10:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Invalid id_token")
    return f"{provider}:stub:{id_token[:8]}"


@router.post("/register", response_model=TokenPair, status_code=status.HTTP_201_CREATED)
def register_password(body: PasswordRegisterRequest) -> TokenPair:
    # HIPAA: do not log email/password.
    log_event(logger, "password_register_stub")
    if body.email in _STUB_USERS:
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail="User already exists")
    _STUB_USERS[body.email] = {
        "hashed_password": hash_password(body.password),
        "role": Role.USER.value,
    }
    return _issue_tokens(subject=body.email, roles=[Role.USER.value])


@router.post("/login", response_model=TokenPair)
def login_password(body: PasswordLoginRequest) -> TokenPair:
    log_event(logger, "password_login_stub")
    user = _STUB_USERS.get(body.email)
    if user is None or not verify_password(body.password, user["hashed_password"]):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid credentials",
        )
    return _issue_tokens(subject=body.email, roles=[user["role"]])


@router.post("/refresh", response_model=TokenPair)
def refresh_tokens(body: RefreshRequest) -> TokenPair:
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
    return _issue_tokens(subject=subject, roles=[Role.USER.value])


@router.post("/google/onetap", response_model=TokenPair)
def google_onetap_exchange(body: OneTapExchangeRequest) -> TokenPair:
    """Stub: exchange Google One Tap ID token for our JWT pair."""
    if body.provider != "google":
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="provider must be google")
    log_event(logger, "google_onetap_exchange_stub")
    subject = _stub_verify_id_token(body.id_token, provider="google")
    return _issue_tokens(subject=subject, roles=[Role.USER.value])


@router.post("/microsoft/onetap", response_model=TokenPair)
def microsoft_onetap_exchange(body: OneTapExchangeRequest) -> TokenPair:
    """Stub: exchange Microsoft One Tap ID token for our JWT pair."""
    if body.provider != "microsoft":
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="provider must be microsoft",
        )
    log_event(logger, "microsoft_onetap_exchange_stub")
    subject = _stub_verify_id_token(body.id_token, provider="microsoft")
    return _issue_tokens(subject=subject, roles=[Role.USER.value])


@router.get("/me", response_model=ProtectedPingResponse)
def auth_me(
    auth: Annotated[AuthContext, Depends(get_current_auth)],
) -> ProtectedPingResponse:
    return ProtectedPingResponse(subject=auth.subject, roles=sorted(auth.roles))


@router.get(
    "/admin/ping",
    response_model=ProtectedPingResponse,
    dependencies=[Depends(require_role(Role.ADMIN))],
)
def admin_ping(
    auth: Annotated[AuthContext, Depends(get_current_auth)],
) -> ProtectedPingResponse:
    return ProtectedPingResponse(
        subject=auth.subject,
        roles=sorted(auth.roles),
        message="admin_ok",
    )
