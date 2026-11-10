"""
Security headers middleware (HIPAA-minded baseline for browser clients).

Sets HSTS (non-local/test), CSP, X-Frame-Options DENY, X-Content-Type-Options,
Referrer-Policy, Permissions-Policy, and Cache-Control: no-store so auth
responses and API JSON are not left in shared caches. Defense-in-depth with
HTTPS at the ALB.
"""

from collections.abc import Callable

from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request
from starlette.responses import Response

from app.core.config import get_settings

# API-oriented CSP: no inline scripts/styles expected from this service itself.
_CSP = (
    "default-src 'self'; "
    "frame-ancestors 'none'; "
    "base-uri 'self'; "
    "form-action 'self'; "
    "object-src 'none'"
)

# FastAPI /docs loads Swagger UI assets from cdn.jsdelivr.net (local/test only).
_DOCS_CSP = (
    "default-src 'self'; "
    "script-src 'self' https://cdn.jsdelivr.net 'unsafe-inline'; "
    "style-src 'self' https://cdn.jsdelivr.net 'unsafe-inline'; "
    "img-src 'self' data: https://cdn.jsdelivr.net; "
    "font-src 'self' https://cdn.jsdelivr.net; "
    "connect-src 'self'; "
    "frame-ancestors 'none'; "
    "base-uri 'self'; "
    "form-action 'self'; "
    "object-src 'none'"
)

_DOC_PATHS = frozenset({"/docs", "/redoc", "/openapi.json"})


class SecurityHeadersMiddleware(BaseHTTPMiddleware):
    async def dispatch(self, request: Request, call_next: Callable) -> Response:
        response = await call_next(request)
        settings = get_settings()

        response.headers.setdefault("X-Content-Type-Options", "nosniff")
        response.headers.setdefault("X-Frame-Options", "DENY")
        response.headers.setdefault("Referrer-Policy", "no-referrer")
        response.headers.setdefault(
            "Permissions-Policy",
            "geolocation=(), microphone=(), camera=()",
        )
        response.headers.setdefault("Cache-Control", "no-store")
        if (
            settings.environment in {"local", "test"}
            and request.url.path in _DOC_PATHS
        ):
            response.headers["Content-Security-Policy"] = _DOCS_CSP
        else:
            response.headers.setdefault("Content-Security-Policy", _CSP)

        # HSTS only outside local/test — otherwise browsers pin HTTPS on localhost.
        if settings.environment not in {"local", "test"}:
            response.headers.setdefault(
                "Strict-Transport-Security",
                "max-age=31536000; includeSubDomains",
            )
        return response
