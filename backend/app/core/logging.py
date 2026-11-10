"""
Structured logging helpers with HIPAA-minded redaction.

Rules of thumb for this codebase:
- Log event *codes* and booleans (e.g. `password_login`, `ses_enabled=true`).
- Never log request bodies, ID tokens, passwords, emails, SSNs, or free-text PHI.
- Prefer `log_event(...)` over raw `logger.info` with kwargs that may be sensitive.

`sanitize_extra` drops known sensitive keys and regex-redacts emails/SSN-like
strings that slip into string values.
"""

from __future__ import annotations

import logging
import re
from typing import Any

# Patterns that must never appear in logs (emails, SSNs, common PHI markers).
_REDACT_PATTERNS: tuple[re.Pattern[str], ...] = (
    re.compile(r"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b"),
    re.compile(r"\b\d{3}-\d{2}-\d{4}\b"),
    re.compile(r"\b\d{9}\b"),
)

_SENSITIVE_KEYS = frozenset(
    {
        "password",
        "token",
        "access_token",
        "refresh_token",
        "id_token",
        "authorization",
        "ssn",
        "email",
        "phone",
        "dob",
        "date_of_birth",
        "medical_record",
        "phi",
        "prompt",
        "content",
        "body",
        # MFA / crypto — never log secrets, codes, or recovery material.
        "mfa_secret",
        "totp",
        "totp_code",
        "otp",
        "otpauth",
        "backup_code",
        "backup_codes",
        "secret",
        "field_encryption_key",
        "fernet_key",
        "cookie",
        # Stripe — never log secret/restricted keys or webhook signing secrets.
        "stripe_secret_key",
        "stripe_webhook_secret",
        "stripe_publishable_key",
        "webhook_secret",
    }
)


def redact_text(value: str) -> str:
    redacted = value
    for pattern in _REDACT_PATTERNS:
        redacted = pattern.sub("[REDACTED]", redacted)
    return redacted


def sanitize_extra(extra: dict[str, Any] | None) -> dict[str, Any]:
    """Drop or mask keys that may carry PHI/PII before logging."""
    if not extra:
        return {}
    clean: dict[str, Any] = {}
    for key, value in extra.items():
        lowered = key.lower()
        if lowered in _SENSITIVE_KEYS or any(s in lowered for s in ("phi", "ssn", "password")):
            clean[key] = "[REDACTED]"
        elif isinstance(value, str):
            clean[key] = redact_text(value)
        else:
            clean[key] = value
    return clean


def get_logger(name: str) -> logging.Logger:
    """Module logger with a simple stream handler (CloudWatch picks up stdout)."""
    logger = logging.getLogger(name)
    if not logger.handlers:
        handler = logging.StreamHandler()
        handler.setFormatter(
            logging.Formatter(
                "%(asctime)s %(levelname)s [%(name)s] %(message)s",
            )
        )
        logger.addHandler(handler)
        logger.setLevel(logging.INFO)
        logger.propagate = False
    return logger


def log_event(logger: logging.Logger, message: str, **extra: Any) -> None:
    """Log an audit-safe event without PHI-bearing fields."""
    logger.info("%s | %s", message, sanitize_extra(extra))
