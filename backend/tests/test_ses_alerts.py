"""SES client + alert templates: stubbed send; PHI forbidden in bodies."""

from __future__ import annotations

import pytest

from app.alerts.login_alert import maybe_send_login_alert
from app.alerts.templates import AlertTemplateError, assert_no_phi, render_login_alert, render_system_alert
from app.clients.ses import DisabledSesClient, SesError, StubSesClient, get_ses_client
from app.core.config import Settings


def test_get_ses_client_disabled_by_default() -> None:
    client = get_ses_client(Settings(ses_enabled=False, ses_from_email=""))
    assert isinstance(client, DisabledSesClient)
    assert client.is_enabled() is False
    with pytest.raises(SesError):
        client.send_email(
            to_addresses=["ops@example.com"],
            subject="x",
            body_text="y",
        )


def test_stub_ses_client_records_send() -> None:
    stub = StubSesClient(enabled=True)
    message_id = stub.send_email(
        to_addresses=["ops@example.com"],
        subject="hello",
        body_text="body",
        body_html="<p>body</p>",
    )
    assert message_id.startswith("stub-message-")
    assert len(stub.sent) == 1
    assert stub.sent[0]["subject"] == "hello"


def test_login_alert_template_has_no_account_identifiers() -> None:
    rendered = render_login_alert(event_code="password_login_success")
    assert "password_login_success" in rendered.body_text
    assert "@" not in rendered.body_text
    assert "PHI" in rendered.body_text or "PII" in rendered.body_text


def test_assert_no_phi_rejects_email_in_body() -> None:
    with pytest.raises(AlertTemplateError):
        assert_no_phi("Contact patient@example.com for details")


def test_assert_no_phi_rejects_ssn_like() -> None:
    with pytest.raises(AlertTemplateError):
        assert_no_phi("SSN 123-45-6789 appeared in payload")


def test_system_alert_rejects_bad_severity() -> None:
    with pytest.raises(AlertTemplateError):
        render_system_alert(alert_code="disk_full", severity="urgent")


def test_maybe_send_login_alert_noop_when_flags_off() -> None:
    stub = StubSesClient(enabled=True)
    sent = maybe_send_login_alert(
        settings=Settings(
            ses_enabled=False,
            login_alert_email_enabled=False,
            ses_from_email="alerts@example.com",
        ),
        ses_client=stub,
    )
    assert sent is False
    assert stub.sent == []


def test_maybe_send_login_alert_uses_stub_when_enabled() -> None:
    stub = StubSesClient(enabled=True)
    sent = maybe_send_login_alert(
        event_code="password_login_success",
        settings=Settings(
            ses_enabled=True,
            ses_from_email="noreply@example.com",
            login_alert_email_enabled=True,
            login_alert_to_email="ops@example.com",
        ),
        ses_client=stub,
    )
    assert sent is True
    assert len(stub.sent) == 1
    assert stub.sent[0]["to_addresses"] == ["ops@example.com"]
    # Body must not include the authenticated user email or ops recipient copy as PHI payload
    # beyond the Destination header — template body itself has no emails.
    assert "@" not in stub.sent[0]["body_text"]


def test_login_route_still_works_with_alert_flag_off(client) -> None:
    register = client.post(
        "/api/v1/auth/register",
        json={"email": "login-alert@example.com", "password": "SecurePass1!x"},
    )
    assert register.status_code == 201
    login = client.post(
        "/api/v1/auth/login",
        json={"email": "login-alert@example.com", "password": "SecurePass1!x"},
    )
    assert login.status_code == 200
    assert "access_token" in login.json()
