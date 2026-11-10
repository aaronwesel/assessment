"""
Signed short-lived media URL stub (S3-minded).

Production would call `boto3.client("s3").generate_presigned_url`. This module
mirrors that contract with an HMAC-signed local URL so tests and local Compose
work without real AWS credentials.

HIPAA:
- Object keys should be opaque (UUIDs), never patient names or MRNs.
- TTL is short by default (`MEDIA_SIGNED_URL_TTL_SECONDS`).
- Do not log the full signed URL (signature is a capability token).
"""

from __future__ import annotations

import hashlib
import hmac
import time
from dataclasses import dataclass
from urllib.parse import quote, urlencode

from app.core.config import get_settings
from app.core.logging import get_logger, log_event

logger = get_logger(__name__)


class MediaUrlError(ValueError):
    pass


@dataclass(frozen=True)
class SignedMediaUrl:
    url: str
    method: str
    object_key: str
    expires_at: int
    ttl_seconds: int


def _signing_secret() -> bytes:
    settings = get_settings()
    # Prefer dedicated media secret; fall back to JWT secret locally only.
    secret = (settings.media_signing_secret or settings.secret_key).encode("utf-8")
    return secret


def _sign(payload: str) -> str:
    return hmac.new(_signing_secret(), payload.encode("utf-8"), hashlib.sha256).hexdigest()


def create_signed_url(
    *,
    object_key: str,
    method: str = "GET",
    ttl_seconds: int | None = None,
    content_type: str | None = None,
) -> SignedMediaUrl:
    """
    Build a short-lived signed media URL.

    `object_key` must be a relative S3-like key (`org/.../uuid.ext`) without `..`.
    """
    settings = get_settings()
    key = object_key.strip().lstrip("/")
    if not key or ".." in key.split("/") or len(key) > 512:
        raise MediaUrlError("invalid object_key")
    method_u = method.upper().strip()
    if method_u not in {"GET", "PUT", "HEAD"}:
        raise MediaUrlError("method must be GET|PUT|HEAD")

    ttl = ttl_seconds if ttl_seconds is not None else settings.media_signed_url_ttl_seconds
    ttl = max(30, min(int(ttl), settings.media_signed_url_max_ttl_seconds))
    expires_at = int(time.time()) + ttl

    # Canonical string similar to S3 SigV4 query auth (simplified for stub).
    canonical = f"{method_u}\n{key}\n{expires_at}"
    if content_type:
        canonical += f"\n{content_type}"
    signature = _sign(canonical)

    base = settings.media_public_base_url.rstrip("/")
    query = {
        "Expires": str(expires_at),
        "Signature": signature,
        "Method": method_u,
    }
    if content_type:
        query["ContentType"] = content_type
    url = f"{base}/{quote(key, safe='/')}?{urlencode(query)}"

    log_event(
        logger,
        "media_signed_url_created",
        method=method_u,
        ttl_seconds=ttl,
        # Log key length only — keys can still be sensitive paths.
        key_len=len(key),
    )
    return SignedMediaUrl(
        url=url,
        method=method_u,
        object_key=key,
        expires_at=expires_at,
        ttl_seconds=ttl,
    )


def verify_signed_url(
    *,
    object_key: str,
    method: str,
    expires_at: int,
    signature: str,
    content_type: str | None = None,
    now: int | None = None,
) -> bool:
    """Validate a stub signature; returns False when expired or tampered."""
    moment = int(time.time() if now is None else now)
    if expires_at < moment:
        return False
    method_u = method.upper().strip()
    canonical = f"{method_u}\n{object_key.strip().lstrip('/')}\n{expires_at}"
    if content_type:
        canonical += f"\n{content_type}"
    expected = _sign(canonical)
    return hmac.compare_digest(expected, signature)
