"""Redis client stub for sessions, rate limits, and token blacklist."""

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

    def blacklist_token(self, jti: str, ttl_seconds: int) -> None:
        self.set(f"blacklist:{jti}", "1", ex=ttl_seconds)

    def is_blacklisted(self, jti: str) -> bool:
        return self.get(f"blacklist:{jti}") is not None


_redis_stub: RedisClientStub | None = None


def get_redis() -> RedisClientStub:
    global _redis_stub
    if _redis_stub is None:
        _redis_stub = RedisClientStub()
        _redis_stub.connect()
    return _redis_stub
