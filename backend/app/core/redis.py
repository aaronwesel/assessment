"""
Redis client for sessions, rate limits, and JWT blacklist.

Production (ElastiCache) stores:
- `session:access:{jti}` / `session:refresh:{jti}` — live token registry
- `blacklist:{jti}` — revoked tokens until natural expiry
- `ratelimit:{client}:{bucket}` — fixed-window counters (cross-task when sticky OFF)
- `login:fail:*` / `login:lock:*` — password abuse counters (opaque email hashes)

Refresh requires the session key to still exist (replay after revoke fails).
Local/dev: in-memory stub if Redis unreachable. Do not log connection URLs.
HIPAA: Redis must never hold PHI payloads — only opaque ids and flags.
"""

from __future__ import annotations

from typing import Any

from app.core.config import get_settings
from app.core.logging import get_logger

logger = get_logger(__name__)


class RedisClientStub:
    """In-memory stand-in when Redis is unavailable; real client swaps in later."""

    def __init__(self) -> None:
        self._store: dict[str, Any] = {}
        self._client: Any | None = None
        self._connected = False

    def connect(self) -> None:
        settings = get_settings()
        try:
            import redis

            client = redis.from_url(settings.redis_url, decode_responses=True)
            client.ping()
            self._client = client
            self._connected = True
            logger.info("Redis connected")
        except Exception:
            # Local/dev without Redis: keep in-memory stub. Do not log connection strings.
            self._client = None
            self._connected = False
            logger.info("Redis unavailable; using in-memory stub")

    @property
    def is_connected(self) -> bool:
        return self._connected

    def set(self, key: str, value: str, ex: int | None = None) -> None:
        if self._client is not None:
            self._client.set(key, value, ex=ex)
            return
        self._store[key] = value

    def get(self, key: str) -> str | None:
        if self._client is not None:
            return self._client.get(key)
        return self._store.get(key)

    def delete(self, key: str) -> None:
        if self._client is not None:
            self._client.delete(key)
            return
        self._store.pop(key, None)

    def incr(self, key: str, ex: int | None = None) -> int:
        """Atomic increment for rate-limit counters. Sets TTL on first create when ``ex`` given."""
        if self._client is not None:
            value = int(self._client.incr(key))
            if ex is not None and value == 1:
                self._client.expire(key, ex)
            return value
        current = int(self._store.get(key, "0") or "0") + 1
        self._store[key] = str(current)
        return current

    def blacklist_token(self, jti: str, ttl_seconds: int) -> None:
        """Mark a JWT id revoked; TTL mirrors remaining token lifetime."""
        self.set(f"blacklist:{jti}", "1", ex=ttl_seconds)

    def is_blacklisted(self, jti: str) -> bool:
        return self.get(f"blacklist:{jti}") is not None

    def has_session(self, token_type: str, jti: str) -> bool:
        """True if the session registry still tracks this jti (used on refresh)."""
        return self.get(f"session:{token_type}:{jti}") is not None


_redis_stub: RedisClientStub | None = None


def get_redis() -> RedisClientStub:
    """Process-wide Redis wrapper (lazy connect on first use)."""
    global _redis_stub
    if _redis_stub is None:
        _redis_stub = RedisClientStub()
        _redis_stub.connect()
    return _redis_stub


def reset_redis_for_tests() -> None:
    """Drop the singleton so tests get a fresh in-memory store."""
    global _redis_stub
    _redis_stub = None
