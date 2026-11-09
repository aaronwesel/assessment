"""Pytest fixtures. Uses SQLite so smoke tests need no Postgres."""

from __future__ import annotations

import os

import pytest
from fastapi.testclient import TestClient

# Configure settings before app import (get_settings is lru_cached).
os.environ.setdefault("ENVIRONMENT", "test")
os.environ.setdefault("SECRET_KEY", "test-secret-key-not-for-production")
os.environ.setdefault("DATABASE_URL", "sqlite:///:memory:")
os.environ.setdefault("REDIS_URL", "redis://localhost:6379/15")
os.environ.setdefault("DEEPSEEK_ENABLED", "false")
os.environ.setdefault("REKOGNITION_ENABLED", "false")
os.environ.setdefault("SES_ENABLED", "false")
os.environ.setdefault("LOGIN_ALERT_EMAIL_ENABLED", "false")
os.environ.setdefault("CORS_ORIGINS", "http://localhost:5173")

from app.core.config import get_settings

get_settings.cache_clear()

from app.main import create_app  # noqa: E402


@pytest.fixture()
def client() -> TestClient:
    application = create_app()
    with TestClient(application) as test_client:
        yield test_client
