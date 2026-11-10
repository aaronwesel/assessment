"""Password complexity policy and lightweight common-password checks.

HIPAA-minded baseline (not a substitute for enterprise IdP / HIBP API):
- bcrypt hashing lives in ``app.core.security`` (already used; argon2 not required).
- Reject weak / common passwords before we persist a hash.
- Never log the candidate password — callers must only log event codes.
"""

from __future__ import annotations

import re

# Compact denylist of extremely common passwords (lowercase). Prefer a remote
# breach corpus (e.g. HIBP k-anonymity) in production; this is a local stub.
_COMMON_PASSWORDS: frozenset[str] = frozenset(
    {
        "password",
        "password1",
        "password123",
        "password123!",
        "12345678",
        "123456789",
        "1234567890",
        "qwerty123",
        "qwertyuiop",
        "letmein1",
        "welcome1",
        "admin123",
        "admin1234",
        "iloveyou1",
        "abc12345",
        "changeme",
        "changeme1",
        "securepass1",  # former fixture password — force stronger test fixtures too
        "passw0rd",
        "monkey123",
        "dragon123",
        "master123",
        "login1234",
        "football1",
        "baseball1",
        "sunshine1",
        "princess1",
        "trustno1",
        "access123",
    }
)

_UPPER = re.compile(r"[A-Z]")
_LOWER = re.compile(r"[a-z]")
_DIGIT = re.compile(r"\d")
_SPECIAL = re.compile(r"[^A-Za-z0-9]")

# Minimum length for new passwords (NIST SP 800-63B allows 8; we prefer 12).
MIN_PASSWORD_LENGTH = 12
MAX_PASSWORD_LENGTH = 128


class PasswordPolicyError(ValueError):
    """Raised when a candidate password fails complexity / denylist checks."""


def validate_password_complexity(password: str, *, email: str | None = None) -> None:
    """Validate password strength. Raises ``PasswordPolicyError`` on failure.

    Rules:
    - length between MIN and MAX
    - at least one upper, lower, digit, and special character
    - not in the local common-password denylist
    - must not equal the email local-part (case-insensitive)
    """
    if len(password) < MIN_PASSWORD_LENGTH:
        raise PasswordPolicyError(
            f"Password must be at least {MIN_PASSWORD_LENGTH} characters"
        )
    if len(password) > MAX_PASSWORD_LENGTH:
        raise PasswordPolicyError(
            f"Password must be at most {MAX_PASSWORD_LENGTH} characters"
        )
    if not _UPPER.search(password):
        raise PasswordPolicyError("Password must include an uppercase letter")
    if not _LOWER.search(password):
        raise PasswordPolicyError("Password must include a lowercase letter")
    if not _DIGIT.search(password):
        raise PasswordPolicyError("Password must include a digit")
    if not _SPECIAL.search(password):
        raise PasswordPolicyError("Password must include a special character")

    lowered = password.lower()
    if lowered in _COMMON_PASSWORDS:
        raise PasswordPolicyError("Password is too common; choose a different one")

    if email:
        local = email.split("@", 1)[0].lower().strip()
        if local and local == lowered:
            raise PasswordPolicyError("Password must not match the email local-part")
