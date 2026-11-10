"""
Slack Incoming Webhook client — optional, feature-flagged, PHI-free.

Why a webhook (not a Slack app) for prep:
- Incoming webhooks are one URL in Secrets Manager; no OAuth dance.
- Enough for “team invite / deploy alert / PR handoff” style posts.
- A full Slack app (Bot Token + Events API) can replace this later for
  interactive handoffs, slash commands, and per-channel routing — see
  docs/team-management.md.

HIPAA: never put PHI (or invite emails, names, MRNs, etc.) in Slack text.
Templates use opaque ids and short snake_case event codes only.
"""

from __future__ import annotations

import re
from typing import Any, Protocol

import httpx

from app.alerts.templates import AlertTemplateError, assert_no_phi
from app.core.config import Settings, get_settings
from app.core.logging import get_logger

logger = get_logger(__name__)

# Event codes allowed in Slack payloads (keep tight to avoid free-text leaks).
_EVENT_RE = re.compile(r"^[a-z][a-z0-9_]{0,63}$")
_ID_RE = re.compile(r"^[a-zA-Z0-9_-]{1,64}$")


class SlackError(RuntimeError):
    pass


class SlackNotifier(Protocol):
    """Minimal notify surface — tests stub this instead of calling Slack."""

    def is_enabled(self) -> bool: ...

    def notify_team_invite(
        self,
        *,
        event: str,
        team_id: str,
        workstream: str,
        role: str,
    ) -> bool: ...

    def notify_deploy_alert(
        self,
        *,
        event: str,
        environment: str,
        severity: str = "info",
    ) -> bool: ...

    def notify_pr_handoff(
        self,
        *,
        event: str,
        workstream: str,
        pr_number: int | None = None,
    ) -> bool: ...


def _require_safe_token(value: str, *, field: str) -> str:
    """Reject anything that is not a short opaque token (blocks emails etc.)."""
    cleaned = value.strip()
    if not _EVENT_RE.match(cleaned) and not _ID_RE.match(cleaned):
        raise AlertTemplateError(f"Slack {field} must be a short opaque token")
    assert_no_phi(cleaned, field=field)
    return cleaned


class DisabledSlackClient:
    """Default when SLACK_ENABLED=false — never contacts the network."""

    def is_enabled(self) -> bool:
        return False

    def notify_team_invite(
        self,
        *,
        event: str,
        team_id: str,
        workstream: str,
        role: str,
    ) -> bool:
        _ = (event, team_id, workstream, role)
        logger.info("Slack notify skipped: feature disabled")
        return False

    def notify_deploy_alert(
        self,
        *,
        event: str,
        environment: str,
        severity: str = "info",
    ) -> bool:
        _ = (event, environment, severity)
        logger.info("Slack notify skipped: feature disabled")
        return False

    def notify_pr_handoff(
        self,
        *,
        event: str,
        workstream: str,
        pr_number: int | None = None,
    ) -> bool:
        _ = (event, workstream, pr_number)
        logger.info("Slack notify skipped: feature disabled")
        return False


class IncomingWebhookSlackClient:
    """
    Posts JSON `{"text": "..."}` to an Incoming Webhook URL.

    Failures are logged at info/warning without raising into request paths —
    team APIs must remain available when Slack is misconfigured.
    """

    def __init__(self, settings: Settings, *, timeout_seconds: float = 5.0) -> None:
        self._settings = settings
        self._timeout = timeout_seconds

    def is_enabled(self) -> bool:
        return bool(self._settings.slack_enabled and self._settings.slack_webhook_url)

    def _post_text(self, text: str) -> bool:
        if not self.is_enabled():
            logger.info("Slack post rejected: feature disabled or webhook missing")
            return False
        # Defense in depth: re-check PHI before any network call.
        assert_no_phi(text, field="slack_text")
        try:
            response = httpx.post(
                self._settings.slack_webhook_url,
                json={"text": text},
                timeout=self._timeout,
            )
            if response.status_code >= 400:
                # Do not log webhook URL or body (URL is a secret).
                logger.warning(
                    "Slack webhook returned error status",
                    extra={"status_code": response.status_code},
                )
                return False
            return True
        except httpx.HTTPError:
            logger.warning("Slack webhook request failed")
            return False

    def notify_team_invite(
        self,
        *,
        event: str,
        team_id: str,
        workstream: str,
        role: str,
    ) -> bool:
        # Opaque ids only — never invite emails or display names.
        safe_event = _require_safe_token(event, field="event")
        safe_team = _require_safe_token(team_id, field="team_id")
        safe_stream = _require_safe_token(workstream, field="workstream")
        safe_role = _require_safe_token(role, field="role")
        text = (
            f"[assessment] team event={safe_event} "
            f"team_id={safe_team} workstream={safe_stream} role={safe_role}"
        )
        return self._post_text(text)

    def notify_deploy_alert(
        self,
        *,
        event: str,
        environment: str,
        severity: str = "info",
    ) -> bool:
        if severity not in {"info", "warning", "critical"}:
            raise AlertTemplateError("severity must be info|warning|critical")
        safe_event = _require_safe_token(event, field="event")
        safe_env = _require_safe_token(environment, field="environment")
        text = (
            f"[assessment] deploy/alert event={safe_event} "
            f"env={safe_env} severity={severity}"
        )
        return self._post_text(text)

    def notify_pr_handoff(
        self,
        *,
        event: str,
        workstream: str,
        pr_number: int | None = None,
    ) -> bool:
        safe_event = _require_safe_token(event, field="event")
        safe_stream = _require_safe_token(workstream, field="workstream")
        # PR number is a public forge identifier, not PHI — still bound it.
        if pr_number is not None and (pr_number < 1 or pr_number > 2_000_000):
            raise AlertTemplateError("pr_number out of allowed range")
        pr_part = f" pr=#{pr_number}" if pr_number is not None else ""
        text = (
            f"[assessment] pr_handoff event={safe_event} "
            f"workstream={safe_stream}{pr_part}"
        )
        return self._post_text(text)


class StubSlackClient:
    """In-memory notify stub for pytest — records calls, no network."""

    def __init__(self, *, enabled: bool = True) -> None:
        self._enabled = enabled
        self.sent: list[dict[str, Any]] = []

    def is_enabled(self) -> bool:
        return self._enabled

    def notify_team_invite(
        self,
        *,
        event: str,
        team_id: str,
        workstream: str,
        role: str,
    ) -> bool:
        if not self._enabled:
            return False
        # Mirror production guards so tests catch PHI regressions.
        _require_safe_token(event, field="event")
        _require_safe_token(team_id, field="team_id")
        _require_safe_token(workstream, field="workstream")
        _require_safe_token(role, field="role")
        record = {
            "kind": "team_invite",
            "event": event,
            "team_id": team_id,
            "workstream": workstream,
            "role": role,
        }
        self.sent.append(record)
        return True

    def notify_deploy_alert(
        self,
        *,
        event: str,
        environment: str,
        severity: str = "info",
    ) -> bool:
        if not self._enabled:
            return False
        record = {
            "kind": "deploy_alert",
            "event": event,
            "environment": environment,
            "severity": severity,
        }
        self.sent.append(record)
        return True

    def notify_pr_handoff(
        self,
        *,
        event: str,
        workstream: str,
        pr_number: int | None = None,
    ) -> bool:
        if not self._enabled:
            return False
        record = {
            "kind": "pr_handoff",
            "event": event,
            "workstream": workstream,
            "pr_number": pr_number,
        }
        self.sent.append(record)
        return True


def get_slack_client(settings: Settings | None = None) -> SlackNotifier:
    """Factory: webhook client when enabled + URL set; else disabled no-op."""
    cfg = settings or get_settings()
    if cfg.slack_enabled and cfg.slack_webhook_url:
        return IncomingWebhookSlackClient(cfg)
    return DisabledSlackClient()
