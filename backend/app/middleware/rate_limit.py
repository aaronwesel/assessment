"""Optional Redis-backed rate limit for abuse protection behind the ALB.

Disabled by default (`RATE_LIMIT_ENABLED=false`). When enabled, uses the shared
Redis client (ElastiCache in AWS; in-memory stub locally) so counters are
consistent across all API tasks — required when sticky sessions are OFF.

Key design:
  - Bucket by client IP (or `X-Forwarded-For` first hop from the ALB)
  - Fixed window per `RATE_LIMIT_WINDOW_SECONDS`
  - Never logs the raw IP into PHI-bearing fields; only structured counters

WAF remains the first line of defense; this is an application safety net.
"""

from __future__ import annotations

from collections.abc import Callable

from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request
from starlette.responses import JSONResponse, Response

from app.core.config import get_settings
from app.core.redis import get_redis

# Paths that must stay reachable for ALB/ECS probes even under load.
_EXEMPT_PREFIXES = ("/health", "/docs", "/openapi.json")


def _client_key(request: Request) -> str:
    forwarded = request.headers.get("x-forwarded-for")
    if forwarded:
        # ALB appends client IP first; take the left-most hop.
        return forwarded.split(",")[0].strip() or "unknown"
    if request.client and request.client.host:
        return request.client.host
    return "unknown"


class RateLimitMiddleware(BaseHTTPMiddleware):
    async def dispatch(self, request: Request, call_next: Callable) -> Response:
        settings = get_settings()
        if not settings.rate_limit_enabled:
            return await call_next(request)

        path = request.url.path
        if any(path == p or path.startswith(f"{p}/") for p in _EXEMPT_PREFIXES):
            return await call_next(request)

        redis = get_redis()
        key = f"ratelimit:{_client_key(request)}:{path.split('/')[1] if path.startswith('/') else 'root'}"
        count = redis.incr(key, ex=settings.rate_limit_window_seconds)
        if count > settings.rate_limit_requests:
            return JSONResponse(
                status_code=429,
                content={"detail": "rate_limit_exceeded"},
                headers={"Retry-After": str(settings.rate_limit_window_seconds)},
            )
        return await call_next(request)
