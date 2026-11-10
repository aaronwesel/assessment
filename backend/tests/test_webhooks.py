"""Webhook outbox enqueue, HMAC signature, and retry delivery."""

from __future__ import annotations

from fastapi.testclient import TestClient

from app.core.redis import reset_redis_for_tests
from app.flags.service import set_flag
from app.webhooks.hmac import sign_body, verify_signature
from tests.helpers_auth import admin_auth_header


def test_hmac_roundtrip() -> None:
    body = b'{"event":"demo"}'
    secret = "test-webhook-secret"
    header = sign_body(body, secret=secret)
    assert header.startswith("sha256=")
    assert verify_signature(body, secret=secret, header_value=header)
    assert not verify_signature(body + b"x", secret=secret, header_value=header)


def test_outbox_enqueue_and_process(client: TestClient) -> None:
    reset_redis_for_tests()
    set_flag("webhooks", True)
    headers = admin_auth_header(client, email="webhook-admin@example.com")

    created = client.post(
        "/api/v1/webhooks/outbox",
        headers=headers,
        json={
            "event_type": "team_updated",
            "destination_url": "https://hooks.example.test/intake",
            "payload": {"team_id": "team_123", "change": "member_added"},
        },
    )
    assert created.status_code == 201, created.text
    row = created.json()
    assert row["status"] == "pending"
    assert row["destination_host"] == "hooks.example.test"
    assert "https://" not in str(row)  # full URL redacted from API response

    captured: list[tuple[str, bytes, dict[str, str]]] = []

    def fake_post(url: str, body: bytes, hdrs: dict[str, str]) -> tuple[int, str]:
        captured.append((url, body, hdrs))
        return 200, "OK"

    # Process via service with injected transport (route uses real httpx).
    from sqlmodel import Session

    from app.db.session import engine
    from app.webhooks import service as webhook_service

    with Session(engine) as session:
        results = webhook_service.process_due(session, limit=10, http_post=fake_post)

    assert len(results) == 1
    assert results[0].status == "delivered"
    assert captured
    assert captured[0][2]["X-Assessment-Signature"].startswith("sha256=")
    assert captured[0][2]["X-Assessment-Event"] == "team_updated"


def test_outbox_retries_on_failure(client: TestClient) -> None:
    reset_redis_for_tests()
    set_flag("webhooks", True)
    headers = admin_auth_header(client, email="webhook-retry@example.com")
    created = client.post(
        "/api/v1/webhooks/outbox",
        headers=headers,
        json={
            "event_type": "deploy_alert",
            "destination_url": "https://hooks.example.test/fail",
            "payload": {"code": "deploy_failed"},
        },
    )
    assert created.status_code == 201
    outbox_id = created.json()["id"]

    from sqlmodel import Session

    from app.db.session import engine
    from app.webhooks import service as webhook_service
    from app.webhooks.models import WebhookOutbox

    def fail_post(url: str, body: bytes, hdrs: dict[str, str]) -> tuple[int, str]:
        return 500, "ERR"

    with Session(engine) as session:
        row = session.get(WebhookOutbox, outbox_id)
        assert row is not None
        updated = webhook_service.deliver_one(session, row, http_post=fail_post)
        assert updated.status == "pending"
        assert updated.attempts == 1
        assert updated.last_error_code == "http_500"
        assert updated.next_attempt_at is not None
