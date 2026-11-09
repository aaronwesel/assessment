"""Slack webhook client: stub notify, PHI guards, flag-off noop."""

from __future__ import annotations

import pytest

from app.alerts.templates import AlertTemplateError
from app.clients.slack import (
    DisabledSlackClient,
    StubSlackClient,
    get_slack_client,
)
from app.core.config import Settings


def test_get_slack_client_disabled_by_default() -> None:
    client = get_slack_client(Settings(slack_enabled=False, slack_webhook_url=""))
    assert isinstance(client, DisabledSlackClient)
    assert client.is_enabled() is False
    assert (
        client.notify_team_invite(
            event="team_invite",
            team_id="team-1",
            workstream="api",
            role="developer",
        )
        is False
    )


def test_stub_slack_records_team_invite_without_email() -> None:
    stub = StubSlackClient(enabled=True)
    ok = stub.notify_team_invite(
        event="team_invite",
        team_id="team_abc",
        workstream="api",
        role="developer",
    )
    assert ok is True
    assert len(stub.sent) == 1
    record = stub.sent[0]
    assert record["kind"] == "team_invite"
    # No email/PII keys in the recorded payload.
    assert "email" not in record
    assert "@" not in str(record)


def test_stub_rejects_email_shaped_token() -> None:
    stub = StubSlackClient(enabled=True)
    with pytest.raises(AlertTemplateError):
        stub.notify_team_invite(
            event="team_invite",
            team_id="patient@example.com",
            workstream="api",
            role="developer",
        )


def test_stub_deploy_and_pr_handoff() -> None:
    stub = StubSlackClient(enabled=True)
    assert stub.notify_deploy_alert(event="deploy_start", environment="dev", severity="info")
    assert stub.notify_pr_handoff(event="pr_ready", workstream="ui", pr_number=42)
    assert len(stub.sent) == 2
    assert stub.sent[1]["pr_number"] == 42


def test_disabled_stub_noop() -> None:
    stub = StubSlackClient(enabled=False)
    assert stub.notify_pr_handoff(event="pr_ready", workstream="infra") is False
    assert stub.sent == []
