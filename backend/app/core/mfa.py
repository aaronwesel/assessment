"""TOTP MFA helpers (pyotp) + hashed backup codes.

Security notes:
- TOTP secrets are stored Fernet-encrypted at rest (``encrypt_field``), never logged.
- Backup codes are bcrypt-hashed (same ``hash_password`` as account passwords).
- Plaintext backup codes are returned **once** at enrollment confirm time.
- Verification uses a ±1 step window to tolerate small clock skew.
"""

from __future__ import annotations

import secrets
from typing import Iterable

import pyotp

from app.core.encryption import decrypt_field, encrypt_field
from app.core.security import hash_password, verify_password

BACKUP_CODE_COUNT = 8
BACKUP_CODE_BYTES = 5  # ~10 hex chars after encoding


def generate_totp_secret() -> str:
    """Return a new base32 TOTP secret (store encrypted — never log)."""
    return pyotp.random_base32()


def encrypt_totp_secret(secret: str) -> str:
    return encrypt_field(secret)


def decrypt_totp_secret(encrypted: str) -> str:
    return decrypt_field(encrypted)


def provisioning_uri(*, secret: str, email: str, issuer: str = "Assessment") -> str:
    """otpauth:// URI for authenticator apps. Do not log the URI (contains secret)."""
    totp = pyotp.TOTP(secret)
    return totp.provisioning_uri(name=email, issuer_name=issuer)


def verify_totp(secret: str, code: str, *, valid_window: int = 1) -> bool:
    """Validate a 6-digit TOTP code against the secret."""
    cleaned = (code or "").strip().replace(" ", "")
    if not cleaned.isdigit() or len(cleaned) != 6:
        return False
    totp = pyotp.TOTP(secret)
    return bool(totp.verify(cleaned, valid_window=valid_window))


def generate_backup_codes(count: int = BACKUP_CODE_COUNT) -> list[str]:
    """Generate one-time recovery codes (return plaintext once; store hashes only)."""
    codes: list[str] = []
    for _ in range(count):
        codes.append(secrets.token_hex(BACKUP_CODE_BYTES))
    return codes


def hash_backup_codes(codes: Iterable[str]) -> list[str]:
    return [hash_password(code) for code in codes]


def verify_backup_code(candidate: str, hashed_codes: Iterable[str]) -> str | None:
    """Return the matching hash if ``candidate`` verifies; else None.

    Caller should remove the matched hash so the code cannot be reused.
    """
    cleaned = (candidate or "").strip().lower()
    if not cleaned:
        return None
    for hashed in hashed_codes:
        if verify_password(cleaned, hashed):
            return hashed
    return None
