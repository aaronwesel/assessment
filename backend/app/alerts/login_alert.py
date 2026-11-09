"""
Optional login-alert email hook. OFF by default; never blocks the auth response.

When `LOGIN_ALERT_EMAIL_ENABLED` and SES are on, successful logins fire a
best-effort ops email. Failures are swallowed so JWT issuance is never delayed
by SES outages.

HIPAA: recipients come from config (ops mailboxes), not the authenticated user.
Bodies use `render_login_alert` — event codes + timestamps only, no PHI/PII.
"""

from __future__ import annotations

from typing import TYPE_CHECKING

from app.alerts.templates import AlertTemplateError, render_login_alert
from app.clients.ses import SesError, SesSender, get_ses_client
from app.core.config import Settings, get_settings
from app.core.logging import get_logger, log_event

if TYPE_CHECKING:
    pass

logger = get_logger(__name__)


def maybe_send_login_alert(
    *,
    event_code: str = "login_success",
    settings: Settings | None = None,
    ses_client: SesSender | None = None,
) -> bool:
    """
    Fire-and-forget login alert when LOGIN_ALERT_EMAIL_ENABLED and SES are on.

    Returns True if a send was attempted successfully. Failures are logged and
    swallowed so auth flows are never blocked by email. Does not accept or
    embed the authenticated subject (no PHI/PII in the body).
    """
    cfg = settings or get_settings()
    if not cfg.login_alert_email_enabled:
        return False
    if not cfg.ses_enabled:
        log_event(logger, "login_alert_skipped_ses_disabled")
        return False

    recipients = [
        addr.strip()
        for addr in cfg.login_alert_to_email.split(",")
        if addr.strip()
    ]
    if not recipients:
        # Fall back to from-address as a self-notify ops mailbox placeholder.
        if cfg.ses_from_email:
            recipients = [cfg.ses_from_email]
        else:
            log_event(logger, "login_alert_skipped_no_recipient")
            return False

    client = ses_client if ses_client is not None else get_ses_client(cfg)
    if not client.is_enabled():
        log_event(logger, "login_alert_skipped_client_disabled")
        return False

    try:
        rendered = render_login_alert(event_code=event_code)
        client.send_email(
            to_addresses=recipients,
            subject=rendered.subject,
            body_text=rendered.body_text,
            body_html=rendered.body_html,
        )
    except (AlertTemplateError, SesError) as exc:
        # Do not log exception text if it might echo addresses; use event code only.
        log_event(logger, "login_alert_send_failed", error_type=type(exc).__name__)
        return False
    except Exception:
        log_event(logger, "login_alert_send_failed", error_type="unexpected")
        return False

    log_event(logger, "login_alert_sent", event_code=event_code)
    return True
