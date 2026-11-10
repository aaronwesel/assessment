"""
Break-glass alert hooks — SES + Slack, PHI-free.

Real sends only when SES/Slack are enabled and feature flags allow them.
Failures are logged as codes; request paths should still succeed after grant.
"""

from __future__ import annotations

from app.alerts.templates import render_system_alert
from app.clients.ses import SesError, get_ses_client
from app.clients.slack import get_slack_client
from app.core.config import get_settings
from app.core.logging import get_logger, log_event
from app.flags.service import is_feature_enabled

logger = get_logger(__name__)


def notify_break_glass_granted(*, grant_id: str, reason_code: str) -> dict[str, bool]:
    """
    Fire optional SES + Slack alerts for a new break-glass grant.

    Bodies use opaque grant id + reason_code only — never target emails/names.
    """
    _ = grant_id  # retained for future correlation without embedding in templates
    settings = get_settings()
    results = {"ses": False, "slack": False}

    if is_feature_enabled("ses") and settings.ses_enabled and settings.login_alert_to_email:
        try:
            rendered = render_system_alert(
                alert_code="break_glass_granted",
                severity="critical",
            )
            # Append reason_code in a controlled way after template PHI assert.
            body = f"{rendered.body_text}Reason: {reason_code}\n"
            client = get_ses_client(settings)
            recipients = [
                addr.strip()
                for addr in settings.login_alert_to_email.split(",")
                if addr.strip()
            ]
            if recipients and client.is_enabled():
                client.send_email(
                    to_addresses=recipients,
                    subject=rendered.subject,
                    body_text=body,
                )
                results["ses"] = True
        except (SesError, ValueError):
            log_event(logger, "break_glass_ses_alert_failed")

    if is_feature_enabled("slack") and settings.slack_enabled:
        try:
            slack = get_slack_client(settings)
            results["slack"] = slack.notify_deploy_alert(
                event="break_glass_granted",
                environment=settings.environment,
                severity="critical",
            )
        except ValueError:
            log_event(logger, "break_glass_slack_alert_failed")

    log_event(
        logger,
        "break_glass_alerts_dispatched",
        ses=results["ses"],
        slack=results["slack"],
        reason_code=reason_code,
    )
    return results
