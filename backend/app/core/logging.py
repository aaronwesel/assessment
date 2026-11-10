"""Structured logging helpers. HIPAA: never log PHI or request bodies with PII/PHI."""

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
