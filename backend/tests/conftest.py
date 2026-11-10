"""Pytest fixtures. Uses SQLite so smoke tests need no Postgres."""

from __future__ import annotations

import os

import pytest
from fastapi.testclient import TestClient

# Configure settings before app import (get_settings is lru_cached).
os.environ["ENVIRONMENT"] = "test"
os.environ["SECRET_KEY"] = "test-secret-key-not-for-production"
os.environ["DATABASE_URL"] = "sqlite:///:memory:"
os.environ["REDIS_URL"] = "redis://localhost:6379/15"
os.environ["DEEPSEEK_ENABLED"] = "false"
os.environ["REKOGNITION_ENABLED"] = "false"
os.environ["SES_ENABLED"] = "false"
os.environ["LOGIN_ALERT_EMAIL_ENABLED"] = "false"
os.environ["SLACK_ENABLED"] = "false"
os.environ["SLACK_WEBHOOK_URL"] = ""
os.environ["RATE_LIMIT_ENABLED"] = "false"
os.environ["CORS_ORIGINS"] = "http://localhost:5173"
# Provider IDs present so exchange routes are available; verifiers are mocked in tests.
os.environ["GOOGLE_CLIENT_ID"] = "test-google-client-id.apps.googleusercontent.com"
os.environ["MICROSOFT_CLIENT_ID"] = "00000000-0000-0000-0000-000000000001"
os.environ["MICROSOFT_TENANT_ID"] = "common"

from app.core.config import get_settings

get_settings.cache_clear()

from app.main import create_app  # noqa: E402


@pytest.fixture()
def client() -> TestClient:
    application = create_app()
    with TestClient(application) as test_client:
        yield test_client
