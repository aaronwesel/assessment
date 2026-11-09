"""Login rate-limit and account lockout / backoff stubs (Redis-backed).

Design notes (HIPAA abuse controls — scaffold, not full WAF):
- Failed password attempts increment a Redis counter keyed by a *hash* of the
  email so logs / Redis keys never store raw addresses.
- After ``LOGIN_MAX_FAILURES``, further attempts are rejected with 429 until
  the lockout TTL expires (simple fixed lockout; exponential backoff is stubbed
  via ``backoff_seconds_for_failures`` for future wiring).
- Successful login clears the failure counter.
- Never log passwords, tokens, or raw emails from this module.
"""

from __future__ import annotations

import hashlib

from app.core.config import get_settings
from app.core.redis import RedisClientStub, get_redis


def _email_key(email: str) -> str:
    """Stable opaque key material — SHA-256 of normalized email (not reversible PII in Redis)."""
    digest = hashlib.sha256(email.lower().strip().encode("utf-8")).hexdigest()[:32]
    return f"login:fail:{digest}"


def _lock_key(email: str) -> str:
    digest = hashlib.sha256(email.lower().strip().encode("utf-8")).hexdigest()[:32]
    return f"login:lock:{digest}"


def backoff_seconds_for_failures(failures: int) -> int:
    """Stub exponential backoff schedule (seconds). Used when escalating lockouts.

    failures 1–2 → 0 (no delay), 3–4 → 30s, 5+ → capped lockout window.
    """
    settings = get_settings()
    if failures < 3:
        return 0
    if failures < settings.login_max_failures:
        return 30
    return settings.login_lockout_seconds


def is_login_locked(email: str, redis: RedisClientStub | None = None) -> bool:
    client = redis or get_redis()
    return client.get(_lock_key(email)) is not None


def record_login_failure(email: str, redis: RedisClientStub | None = None) -> int:
    """Increment failure count; engage lockout when threshold reached. Returns new count."""
    settings = get_settings()
    client = redis or get_redis()
    key = _email_key(email)
    count = client.incr(key, ex=settings.login_failure_window_seconds)
    if count >= settings.login_max_failures:
        # Fixed lockout window (backoff stub available via backoff_seconds_for_failures).
        client.set(_lock_key(email), "1", ex=settings.login_lockout_seconds)
    return count


def clear_login_failures(email: str, redis: RedisClientStub | None = None) -> None:
    client = redis or get_redis()
    client.delete(_email_key(email))
    client.delete(_lock_key(email))
