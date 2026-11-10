"""User persistence helpers for password + federated login."""

from __future__ import annotations

from sqlmodel import Session, select

from app.core.logging import get_logger, log_event
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
) -> User:
    normalized = email.lower().strip()
    if get_user_by_email(session, normalized) is not None:
        raise ValueError("User already exists")

    user = User(
        email=normalized,
        hashed_password=hash_password(password),
        full_name=full_name,
        role=UserRole.USER,
        is_active=True,
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
    return [user.role.value if hasattr(user.role, "value") else str(user.role)]
