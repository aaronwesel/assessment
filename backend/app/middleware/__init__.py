"""
ASGI/Starlette middleware stack.

- `request_id` — opaque X-Request-ID for multi-task ALB tracing (no PHI)
- `rate_limit` — optional Redis fixed-window limits (cross-task when sticky OFF)
- `security_headers` — HIPAA-minded browser response headers / no-store
"""
