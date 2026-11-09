"""JWT access/refresh tokens with jti for Redis blacklist/session tracking."""

from __future__ import annotations

from datetime import UTC, datetime, timedelta
from typing import Any, Literal
from uuid import uuid4

import bcrypt
from jose import JWTError, jwt

from app.core.config import get_settings
from app.core.redis import get_redis

TokenType = Literal["access", "refresh"]


def hash_password(password: str) -> str:
    return bcrypt.hashpw(password.encode("utf-8"), bcrypt.gensalt()).decode("utf-8")


def verify_password(plain_password: str, hashed_password: str) -> bool:
    return bcrypt.checkpw(
        plain_password.encode("utf-8"),
        hashed_password.encode("utf-8"),
    )


def _create_token(
    subject: str,
    token_type: TokenType,
    expires_delta: timedelta,
    extra_claims: dict[str, Any] | None = None,
) -> tuple[str, str, int]:
    """Return (encoded_jwt, jti, ttl_seconds)."""
    settings = get_settings()
    now = datetime.now(UTC)
    jti = str(uuid4())
    ttl_seconds = int(expires_delta.total_seconds())
    payload: dict[str, Any] = {
        "sub": subject,
        "type": token_type,
        "jti": jti,
        "iat": now,
        "exp": now + expires_delta,
    }
    if extra_claims:
        # Never put PHI in JWT claims.
        payload.update(extra_claims)
    encoded = jwt.encode(payload, settings.secret_key, algorithm=settings.jwt_algorithm)
    return encoded, jti, ttl_seconds


def create_access_token(subject: str, roles: list[str] | None = None) -> str:
    settings = get_settings()
    token, jti, ttl = _create_token(
        subject=subject,
        token_type="access",
        expires_delta=timedelta(minutes=settings.access_token_expire_minutes),
        extra_claims={"roles": roles or []},
    )
    get_redis().set(f"session:access:{jti}", subject, ex=ttl)
    return token


def create_refresh_token(subject: str) -> str:
    settings = get_settings()
    token, jti, ttl = _create_token(
        subject=subject,
        token_type="refresh",
        expires_delta=timedelta(days=settings.refresh_token_expire_days),
    )
    get_redis().set(f"session:refresh:{jti}", subject, ex=ttl)
    return token


def decode_token(
    token: str,
    expected_type: TokenType | None = None,
    *,
    check_blacklist: bool = True,
) -> dict[str, Any]:
    settings = get_settings()
    try:
        payload = jwt.decode(token, settings.secret_key, algorithms=[settings.jwt_algorithm])
    except JWTError as exc:
        raise ValueError("Invalid token") from exc
    if expected_type and payload.get("type") != expected_type:
        raise ValueError("Unexpected token type")
    jti = payload.get("jti")
    if check_blacklist and jti and get_redis().is_blacklisted(str(jti)):
        raise ValueError("Token revoked")
    return payload


def revoke_token(token: str) -> None:
    """Blacklist a token's jti until its natural expiry (best-effort)."""
    settings = get_settings()
    try:
        payload = jwt.decode(
            token,
            settings.secret_key,
            algorithms=[settings.jwt_algorithm],
            options={"verify_exp": False},
        )
    except JWTError:
        return
    jti = payload.get("jti")
    if not jti:
        return
    exp = payload.get("exp")
    now = datetime.now(UTC).timestamp()
    ttl = max(int(exp - now), 1) if isinstance(exp, (int, float)) else 3600
    redis = get_redis()
    redis.blacklist_token(str(jti), ttl)
    token_type = payload.get("type")
    if token_type == "access":
        redis.delete(f"session:access:{jti}")
    elif token_type == "refresh":
        redis.delete(f"session:refresh:{jti}")
