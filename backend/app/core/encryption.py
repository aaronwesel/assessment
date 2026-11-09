"""Field-level encryption helpers (Fernet / AES-128-CBC+HMAC via cryptography).

Use for small secrets stored in the application database (e.g. MFA TOTP seeds).
Bulk PHI / column encryption at rest should prefer RDS/KMS (see docs/security.md
and Terraform module comments). Do not put raw FERNET keys in git — load from
Secrets Manager / env.

If ``FIELD_ENCRYPTION_KEY`` is unset, we derive a Fernet key from ``SECRET_KEY``
so local/test works without extra config. Production must set an explicit key.
"""

from __future__ import annotations

import base64
import hashlib
from functools import lru_cache

from cryptography.fernet import Fernet, InvalidToken

from app.core.config import get_settings


def _derive_fernet_key(secret: str) -> bytes:
    """Derive a url-safe 32-byte Fernet key from an arbitrary secret string."""
    digest = hashlib.sha256(secret.encode("utf-8")).digest()
    return base64.urlsafe_b64encode(digest)


@lru_cache
def _fernet() -> Fernet:
    settings = get_settings()
    raw = (settings.field_encryption_key or "").strip()
    if raw:
        # Accept either a raw Fernet key or any secret string we can derive from.
        try:
            return Fernet(raw.encode("utf-8") if isinstance(raw, str) else raw)
        except (ValueError, TypeError):
            return Fernet(_derive_fernet_key(raw))
    # Fallback: derive from JWT secret (documented as local/test only).
    return Fernet(_derive_fernet_key(settings.secret_key))


def encrypt_field(plaintext: str) -> str:
    """Encrypt a UTF-8 string; returns a Fernet token (url-safe base64)."""
    return _fernet().encrypt(plaintext.encode("utf-8")).decode("utf-8")


def decrypt_field(token: str) -> str:
    """Decrypt a Fernet token. Raises ValueError if the token is invalid/tampered."""
    try:
        return _fernet().decrypt(token.encode("utf-8")).decode("utf-8")
    except InvalidToken as exc:
        raise ValueError("Invalid encrypted field") from exc


def reset_fernet_cache() -> None:
    """Test helper: clear cached Fernet after settings changes."""
    _fernet.cache_clear()
