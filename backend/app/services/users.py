"""
User persistence helpers for password + federated (Google/Microsoft) login + MFA.

Responsibilities:
- Create password accounts with bcrypt hashes and a `password` AccountLink.
- Enforce password complexity before hashing.
- Authenticate password logins without leaking whether email vs password failed
  at the HTTP layer (routes return a generic 401).
- Upsert federated users by provider subject; link to an existing email row when
  present so One Tap can attach to a prior password account.
- MFA enroll/confirm/verify with Fernet-encrypted TOTP secrets and hashed backup codes.

HIPAA: log only provider/event codes — never emails, tokens, TOTP secrets, or backup codes.
"""

from __future__ import annotations

import json

from sqlmodel import Session, select

from app.core.logging import get_logger, log_event
from app.core.mfa import (
    decrypt_totp_secret,
    encrypt_totp_secret,
    generate_backup_codes,
    generate_totp_secret,
    hash_backup_codes,
    provisioning_uri,
    verify_backup_code,
    verify_totp,
)
from app.core.passwords import PasswordPolicyError, validate_password_complexity
from app.core.security import hash_password, verify_password
from app.models.account_link import AccountLink, IdentityProvider
from app.models.user import User, UserRole

logger = get_logger(__name__)


def get_user_by_email(session: Session, email: str) -> User | None:
    statement = select(User).where(User.email == email.lower())
    return session.exec(statement).first()


def get_user_by_id(session: Session, user_id: str) -> User | None:
    return session.get(User, user_id)


def get_link(
    session: Session,
    *,
    provider: IdentityProvider,
    provider_subject: str,
) -> AccountLink | None:
    statement = select(AccountLink).where(
        AccountLink.provider == provider,
        AccountLink.provider_subject == provider_subject,
    )
    return session.exec(statement).first()


def create_password_user(
    session: Session,
    *,
    email: str,
    password: str,
    full_name: str | None = None,
    mfa_policy_required: bool = False,
) -> User:
    """Register a local password user; raises ValueError if email already exists."""
    normalized = email.lower().strip()
    if get_user_by_email(session, normalized) is not None:
        raise ValueError("User already exists")

    # Policy before hash — never persist weak / common passwords.
    try:
        validate_password_complexity(password, email=normalized)
    except PasswordPolicyError as exc:
        raise ValueError(str(exc)) from exc

    user = User(
        email=normalized,
        hashed_password=hash_password(password),
        full_name=full_name,
        role=UserRole.USER,
        is_active=True,
        mfa_enabled=False,
        mfa_policy_required=bool(mfa_policy_required),
    )
    session.add(user)
    session.flush()

    link = AccountLink(
        user_id=user.id,
        provider=IdentityProvider.PASSWORD,
        provider_subject=normalized,
    )
    session.add(link)
    session.commit()
    session.refresh(user)
    log_event(logger, "user_registered", provider="password")
    return user


def authenticate_password(session: Session, *, email: str, password: str) -> User | None:
    """Return the active user on success, else None (no distinction of failure reason)."""
    user = get_user_by_email(session, email.lower().strip())
    if user is None or not user.is_active:
        return None
    if not user.hashed_password:
        return None
    if not verify_password(password, user.hashed_password):
        return None
    log_event(logger, "password_login_ok")
    return user


def upsert_federated_user(
    session: Session,
    *,
    provider: IdentityProvider,
    provider_subject: str,
    email: str | None,
    full_name: str | None,
) -> User:
    """Link or create a user for a verified Google/Microsoft identity."""
    existing_link = get_link(session, provider=provider, provider_subject=provider_subject)
    if existing_link is not None:
        user = get_user_by_id(session, existing_link.user_id)
        if user is None or not user.is_active:
            raise ValueError("Linked user unavailable")
        log_event(logger, "federated_login_ok", provider=provider.value)
        return user

    user: User | None = None
    if email:
        user = get_user_by_email(session, email.lower().strip())

    if user is None:
        if not email:
            raise ValueError("Federated identity missing email; cannot create user")
        user = User(
            email=email.lower().strip(),
            hashed_password=None,
            full_name=full_name,
            role=UserRole.USER,
            is_active=True,
            mfa_enabled=False,
        )
        session.add(user)
        session.flush()
        log_event(logger, "user_registered", provider=provider.value)
    elif full_name and not user.full_name:
        user.full_name = full_name
        session.add(user)

    link = AccountLink(
        user_id=user.id,
        provider=provider,
        provider_subject=provider_subject,
    )
    session.add(link)
    session.commit()
    session.refresh(user)
    log_event(logger, "federated_login_ok", provider=provider.value)
    return user


def role_values(user: User) -> list[str]:
    """Normalize SQLModel/enum role into JWT claim strings."""
    return [user.role.value if hasattr(user.role, "value") else str(user.role)]


def _load_backup_hashes(user: User) -> list[str]:
    if not user.mfa_backup_codes_hashed:
        return []
    try:
        data = json.loads(user.mfa_backup_codes_hashed)
    except json.JSONDecodeError:
        return []
    if not isinstance(data, list):
        return []
    return [str(item) for item in data]


def _store_backup_hashes(user: User, hashes: list[str]) -> None:
    user.mfa_backup_codes_hashed = json.dumps(hashes)


def begin_mfa_enrollment(session: Session, user: User, *, issuer: str) -> dict[str, str]:
    """Create a pending TOTP secret (encrypted) without enabling MFA yet.

    Returns provisioning material for the authenticator app. The secret must
    be confirmed via ``confirm_mfa_enrollment`` before ``mfa_enabled`` flips.
    """
    secret = generate_totp_secret()
    user.mfa_secret_encrypted = encrypt_totp_secret(secret)
    user.mfa_enabled = False
    session.add(user)
    session.commit()
    session.refresh(user)
    log_event(logger, "mfa_enroll_begun")
    return {
        "secret": secret,
        "otpauth_uri": provisioning_uri(secret=secret, email=user.email, issuer=issuer),
    }


def confirm_mfa_enrollment(
    session: Session,
    user: User,
    *,
    totp_code: str,
) -> list[str]:
    """Verify the first TOTP code, enable MFA, and issue one-time backup codes."""
    if not user.mfa_secret_encrypted:
        raise ValueError("MFA enrollment not started")
    secret = decrypt_totp_secret(user.mfa_secret_encrypted)
    if not verify_totp(secret, totp_code):
        raise ValueError("Invalid TOTP code")

    plaintext_codes = generate_backup_codes()
    _store_backup_hashes(user, hash_backup_codes(plaintext_codes))
    user.mfa_enabled = True
    session.add(user)
    session.commit()
    session.refresh(user)
    log_event(logger, "mfa_enroll_confirmed")
    # Plaintext codes returned once — never logged, never re-fetched.
    return plaintext_codes


def verify_user_mfa(session: Session, user: User, *, totp_code: str | None, backup_code: str | None) -> bool:
    """Validate TOTP or a single-use backup code for an MFA-enabled user."""
    if not user.mfa_enabled or not user.mfa_secret_encrypted:
        return False

    if totp_code:
        secret = decrypt_totp_secret(user.mfa_secret_encrypted)
        if verify_totp(secret, totp_code):
            log_event(logger, "mfa_totp_ok")
            return True

    if backup_code:
        hashes = _load_backup_hashes(user)
        matched = verify_backup_code(backup_code, hashes)
        if matched is not None:
            hashes = [h for h in hashes if h != matched]
            _store_backup_hashes(user, hashes)
            session.add(user)
            session.commit()
            log_event(logger, "mfa_backup_code_ok")
            return True

    log_event(logger, "mfa_verify_failed")
    return False


def disable_mfa(session: Session, user: User) -> None:
    user.mfa_enabled = False
    user.mfa_secret_encrypted = None
    user.mfa_backup_codes_hashed = None
    session.add(user)
    session.commit()
    log_event(logger, "mfa_disabled")
