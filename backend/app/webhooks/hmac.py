"""
HMAC signatures for outbound webhook deliveries.

Recipients verify `X-Assessment-Signature: sha256=<hex>` over the raw body
using the shared `WEBHOOK_SIGNING_SECRET`. Never put PHI in webhook payloads.
"""

from __future__ import annotations

import hashlib
import hmac


def sign_body(body: bytes, *, secret: str) -> str:
    digest = hmac.new(secret.encode("utf-8"), body, hashlib.sha256).hexdigest()
    return f"sha256={digest}"


def verify_signature(body: bytes, *, secret: str, header_value: str) -> bool:
    expected = sign_body(body, secret=secret)
    return hmac.compare_digest(expected, header_value.strip())
