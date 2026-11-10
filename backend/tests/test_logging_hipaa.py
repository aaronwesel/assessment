"""Ensure log sanitization redacts PHI-like fields."""

from app.core.logging import redact_text, sanitize_extra


def test_redact_email_and_ssn() -> None:
    text = "contact patient@example.com ssn 123-45-6789"
    redacted = redact_text(text)
    assert "patient@example.com" not in redacted
    assert "123-45-6789" not in redacted
    assert "[REDACTED]" in redacted


def test_sanitize_extra_masks_sensitive_keys() -> None:
    clean = sanitize_extra(
        {
            "email": "a@b.com",
            "password": "secret",
            "prompt": "patient history...",
            "request_id": "abc-123",
        }
    )
    assert clean["email"] == "[REDACTED]"
    assert clean["password"] == "[REDACTED]"
    assert clean["prompt"] == "[REDACTED]"
    assert clean["request_id"] == "abc-123"
