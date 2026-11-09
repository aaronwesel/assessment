"""
Alert email templates for Amazon SES — PHI forbidden in body by default.

Used by login/system alert hooks (`app.alerts.login_alert`). Safe fields only:
event type, UTC timestamp, coarse context codes. Do not put names, emails,
MRNs, diagnoses, or free-text user content in bodies — `assert_no_phi` guards
rendered copy before send.
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from datetime import UTC, datetime

# Patterns that strongly suggest PHI/PII leaked into a body. Keep conservative.
_EMAIL_RE = re.compile(r"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", re.IGNORECASE)
_SSN_RE = re.compile(r"\b\d{3}-\d{2}-\d{4}\b")
_MRN_RE = re.compile(r"\b(?:MRN|medical\s*record)\s*[:=#]?\s*\w+", re.IGNORECASE)
_PHONE_RE = re.compile(r"\b(?:\+?1[-.\s]?)?\(?\d{3}\)?[-.\s]\d{3}[-.\s]\d{4}\b")
_DOB_RE = re.compile(
    r"\b(?:dob|date\s*of\s*birth)\s*[:=]?\s*\d{1,4}[-/]\d{1,2}[-/]\d{1,4}\b",
    re.IGNORECASE,
)


class AlertTemplateError(ValueError):
    """Raised when a template body fails the no-PHI guard."""


def assert_no_phi(text: str, *, field: str = "body") -> None:
    """
    Reject bodies that look like they contain PHI/PII.

    Templates must stay on allowlisted safe copy. Callers must not concatenate
    user-supplied free text into alert bodies.
    """
    checks = (
        (_EMAIL_RE, "email address"),
        (_SSN_RE, "SSN-like number"),
        (_MRN_RE, "medical record reference"),
        (_PHONE_RE, "phone number"),
        (_DOB_RE, "date of birth"),
    )
    for pattern, label in checks:
        if pattern.search(text):
            raise AlertTemplateError(f"PHI forbidden in alert {field}: detected {label}")


@dataclass(frozen=True)
class RenderedAlert:
    subject: str
    body_text: str
    body_html: str | None = None


def _utc_stamp(when: datetime | None = None) -> str:
    moment = when or datetime.now(UTC)
    if moment.tzinfo is None:
        moment = moment.replace(tzinfo=UTC)
    return moment.astimezone(UTC).strftime("%Y-%m-%d %H:%M:%S UTC")


def render_login_alert(
    *,
    event_code: str = "login_success",
    occurred_at: datetime | None = None,
) -> RenderedAlert:
    """
    Login alert copy — no account identifiers in the body.

    Recipients are chosen by the caller (e.g. configured ops mailbox), not from
    the authenticated subject, so the body stays free of emails/names.
    """
    if not re.fullmatch(r"[a-z][a-z0-9_]{0,63}", event_code):
        raise AlertTemplateError("event_code must be a short snake_case token")

    stamp = _utc_stamp(occurred_at)
    subject = f"[assessment] Security alert: {event_code}"
    body_text = (
        "A security-relevant authentication event was recorded.\n"
        f"Event: {event_code}\n"
        f"Time: {stamp}\n"
        "\n"
        "This message intentionally omits account identifiers and other PHI/PII.\n"
        "Review application audit logs for details.\n"
    )
    body_html = (
        "<p>A security-relevant authentication event was recorded.</p>"
        f"<p>Event: <code>{event_code}</code><br/>Time: {stamp}</p>"
        "<p>This message intentionally omits account identifiers and other PHI/PII. "
        "Review application audit logs for details.</p>"
    )
    assert_no_phi(subject, field="subject")
    assert_no_phi(body_text, field="body_text")
    assert_no_phi(body_html, field="body_html")
    return RenderedAlert(subject=subject, body_text=body_text, body_html=body_html)


def render_system_alert(
    *,
    alert_code: str,
    severity: str = "info",
    occurred_at: datetime | None = None,
) -> RenderedAlert:
    """System/ops alert — codes and severity only; no free-text payloads."""
    if not re.fullmatch(r"[a-z][a-z0-9_]{0,63}", alert_code):
        raise AlertTemplateError("alert_code must be a short snake_case token")
    if severity not in {"info", "warning", "critical"}:
        raise AlertTemplateError("severity must be info|warning|critical")

    stamp = _utc_stamp(occurred_at)
    subject = f"[assessment] System alert ({severity}): {alert_code}"
    body_text = (
        "A system alert was raised.\n"
        f"Code: {alert_code}\n"
        f"Severity: {severity}\n"
        f"Time: {stamp}\n"
        "\n"
        "This message intentionally omits PHI/PII and free-text payloads.\n"
        "Check CloudWatch / audit logs for operational detail.\n"
    )
    assert_no_phi(subject, field="subject")
    assert_no_phi(body_text, field="body_text")
    return RenderedAlert(subject=subject, body_text=body_text, body_html=None)
