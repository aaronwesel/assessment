"""Request-ID middleware for load-balanced tracing across API tasks.

Every inbound request gets a stable id (client-supplied `X-Request-ID` or a new
UUID). The id is:
  - stored on `request.state.request_id` for handlers/logging
  - echoed on the response as `X-Request-ID`

Safe for multi-task ALB fan-out: ids are opaque UUIDs with no PHI. Do not put
emails, tokens, or medical content into request-id headers.
"""

from __future__ import annotations

import uuid
from collections.abc import Callable

from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request
from starlette.responses import Response

REQUEST_ID_HEADER = "X-Request-ID"


def _normalize_request_id(raw: str | None) -> str:
    if not raw:
        return str(uuid.uuid4())
    cleaned = raw.strip()[:128]
    # Reject empty / control characters; fall back to a fresh id.
    if not cleaned or any(ord(ch) < 32 for ch in cleaned):
        return str(uuid.uuid4())
    return cleaned


class RequestIdMiddleware(BaseHTTPMiddleware):
    async def dispatch(self, request: Request, call_next: Callable) -> Response:
        request_id = _normalize_request_id(request.headers.get(REQUEST_ID_HEADER))
        request.state.request_id = request_id
        response = await call_next(request)
        response.headers[REQUEST_ID_HEADER] = request_id
        return response
