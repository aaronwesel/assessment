"""Audit log list + CSV/JSON export — no PHI in payloads."""

from __future__ import annotations

from fastapi.testclient import TestClient

from tests.helpers_auth import admin_auth_header, register_user


def test_audit_requires_admin(client: TestClient) -> None:
    user = register_user(client, email="audit-user@example.com")
    response = client.get(
        "/api/v1/audit/events",
        headers={"Authorization": f"Bearer {user['access_token']}"},
    )
    assert response.status_code == 403


def test_audit_create_list_and_export(client: TestClient) -> None:
    headers = admin_auth_header(client, email="audit-admin@example.com")
    created = client.post(
        "/api/v1/audit/events",
        headers=headers,
        json={
            "action": "ops_note",
            "resource_type": "system",
            "resource_id": "bootstrap",
            "metadata": {"env": "test", "ok": True},
        },
    )
    assert created.status_code == 201, created.text
    body = created.json()
    assert body["action"] == "ops_note"
    assert body["actor_user_id"] == headers["X-User-Id"]
    assert "email" not in (body.get("metadata") or {})

    listed = client.get("/api/v1/audit/events", headers=headers)
    assert listed.status_code == 200
    assert any(item["action"] == "ops_note" for item in listed.json()["items"])

    json_export = client.get("/api/v1/audit/export?format=json", headers=headers)
    assert json_export.status_code == 200
    assert json_export.headers["content-type"].startswith("application/json")
    assert "ops_note" in json_export.text
    assert "@" not in json_export.text  # no emails in export body

    csv_export = client.get("/api/v1/audit/export?format=csv", headers=headers)
    assert csv_export.status_code == 200
    assert "text/csv" in csv_export.headers["content-type"]
    assert "action" in csv_export.text


def test_audit_rejects_phi_metadata(client: TestClient) -> None:
    headers = admin_auth_header(client, email="audit-phi@example.com")
    response = client.post(
        "/api/v1/audit/events",
        headers=headers,
        json={
            "action": "ops_note",
            "resource_type": "system",
            "metadata": {"note": "patient@example.com"},
        },
    )
    assert response.status_code == 400
