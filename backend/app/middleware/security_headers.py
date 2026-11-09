"""
Security headers middleware (HIPAA-minded baseline for browser clients).

Adds nosniff, frame denial, no-referrer, locked-down permissions, and no-store
caching so auth responses and API JSON are not left in shared caches. This is
defense-in-depth alongside HTTPS at the ALB and CSP that the SPA may add later.
"""

from collections.abc import Callable

from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request
from starlette.responses import Response


class SecurityHeadersMiddleware(BaseHTTPMiddleware):
    async def dispatch(self, request: Request, call_next: Callable) -> Response:
        response = await call_next(request)
        response.headers.setdefault("X-Content-Type-Options", "nosniff")
        response.headers.setdefault("X-Frame-Options", "DENY")
        response.headers.setdefault("Referrer-Policy", "no-referrer")
        response.headers.setdefault(
            "Permissions-Policy",
            "geolocation=(), microphone=(), camera=()",
        )
        response.headers.setdefault("Cache-Control", "no-store")
        return response
