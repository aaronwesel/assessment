"""
Application settings loaded from environment / `.env` (no secrets in source).

Why pydantic-settings: a single typed surface for local Compose, ECS task env,
and Secrets Manager–injected values. AliasChoices accept both short and
`*_OAUTH_*` names so Compose and AWS wiring stay interchangeable.

Auth / JWT:
- `secret_key` signs access + refresh tokens (HS256 by default).
- Short access TTL + longer refresh TTL matches Redis session keys in security.py.

Load balancing:
- Optional Redis rate limits (`RATE_LIMIT_*`) stay consistent across ECS tasks
  when ALB sticky sessions are OFF.

HIPAA / safety defaults:
- DeepSeek, Rekognition, SES, and Slack are OFF unless explicitly enabled.
- Login-alert recipients are ops mailboxes, not end-user PHI destinations.
- Slack webhook bodies must stay PHI-free (opaque ids + event codes only).
"""

from functools import lru_cache
from typing import Literal

from pydantic import AliasChoices, Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        case_sensitive=False,
        extra="ignore",
    )

    app_name: str = "assessment-api"
    environment: Literal["local", "dev", "staging", "prod", "test"] = "local"
    debug: bool = False
    api_v1_prefix: str = "/api/v1"
    # Comma-separated origins (string avoids pydantic-settings JSON list parsing).
    cors_origins: str = "http://localhost:5173,http://localhost:3000"

    secret_key: str = Field(
        default="change-me-in-secrets-manager",
        validation_alias=AliasChoices("SECRET_KEY", "JWT_SECRET"),
    )
    access_token_expire_minutes: int = Field(
        default=15,
        validation_alias=AliasChoices("ACCESS_TOKEN_EXPIRE_MINUTES", "JWT_ACCESS_TTL_MINUTES"),
    )
    refresh_token_expire_days: int = Field(
        default=7,
        validation_alias=AliasChoices("REFRESH_TOKEN_EXPIRE_DAYS", "JWT_REFRESH_TTL_DAYS"),
    )
    jwt_algorithm: str = "HS256"

    # Field-level Fernet key (base64 url-safe 32-byte or any secret we derive from).
    # Production: set via Secrets Manager. Empty → derive from secret_key (local/test only).
    field_encryption_key: str = Field(
        default="",
        validation_alias=AliasChoices("FIELD_ENCRYPTION_KEY", "FERNET_KEY"),
    )

    # Login abuse controls (Redis). Tunable without code changes.
    login_max_failures: int = Field(default=5, validation_alias="LOGIN_MAX_FAILURES")
    login_failure_window_seconds: int = Field(
        default=900,
        validation_alias="LOGIN_FAILURE_WINDOW_SECONDS",
    )
    login_lockout_seconds: int = Field(
        default=900,
        validation_alias="LOGIN_LOCKOUT_SECONDS",
    )

    # MFA: when true, password users with mfa_enabled must send totp_code on login.
    mfa_issuer: str = Field(default="Assessment", validation_alias="MFA_ISSUER")

    database_url: str = "postgresql+psycopg://postgres:postgres@localhost:5432/assessment"
    redis_url: str = "redis://localhost:6379/0"

    # Google Identity Services (One Tap) — accept GOOGLE_* and GOOGLE_OAUTH_* names
    google_client_id: str = Field(
        default="",
        validation_alias=AliasChoices("GOOGLE_CLIENT_ID", "GOOGLE_OAUTH_CLIENT_ID"),
    )
    google_client_secret: str = Field(
        default="",
        validation_alias=AliasChoices("GOOGLE_CLIENT_SECRET", "GOOGLE_OAUTH_CLIENT_SECRET"),
    )

    # Microsoft Entra / MSAL (One Tap)
    microsoft_client_id: str = Field(
        default="",
        validation_alias=AliasChoices("MICROSOFT_CLIENT_ID", "MICROSOFT_OAUTH_CLIENT_ID"),
    )
    microsoft_client_secret: str = Field(
        default="",
        validation_alias=AliasChoices(
            "MICROSOFT_CLIENT_SECRET",
            "MICROSOFT_OAUTH_CLIENT_SECRET",
        ),
    )
    microsoft_tenant_id: str = Field(
        default="common",
        validation_alias=AliasChoices("MICROSOFT_TENANT_ID", "MICROSOFT_OAUTH_TENANT_ID"),
    )

    # DeepSeek — OFF by default; never send PHI in prompts
    deepseek_api_key: str = ""
    deepseek_base_url: str = "https://api.deepseek.com"
    deepseek_enabled: bool = False

    # AWS / Rekognition — prefer IAM task role in ECS
    aws_region: str = Field(
        default="",
        validation_alias=AliasChoices("AWS_REGION", "AWS_DEFAULT_REGION"),
    )
    aws_access_key_id: str = ""
    aws_secret_access_key: str = ""
    rekognition_enabled: bool = False

    # Amazon SES — OFF by default; alert bodies must not contain PHI
    ses_from_email: str = ""
    ses_region: str = ""  # falls back to aws_region when empty
    ses_enabled: bool = False
    # Optional login-alert hook (requires SES_ENABLED + recipient)
    login_alert_email_enabled: bool = False
    login_alert_to_email: str = ""  # comma-separated ops mailboxes; not end-user PHI

    # Optional Redis-backed rate limit (cross-task; sticky sessions are OFF)
    rate_limit_enabled: bool = False
    rate_limit_requests: int = 120
    rate_limit_window_seconds: int = 60

    # Slack Incoming Webhook — OFF by default; never put PHI in Slack messages
    slack_webhook_url: str = Field(
        default="",
        validation_alias=AliasChoices("SLACK_WEBHOOK_URL"),
    )
    slack_enabled: bool = Field(
        default=False,
        validation_alias=AliasChoices("SLACK_ENABLED"),
    )

    # Stripe Billing (test mode) — secrets via env / Secrets Manager only
    stripe_secret_key: str = Field(
        default="",
        validation_alias=AliasChoices("STRIPE_SECRET_KEY"),
    )
    stripe_publishable_key: str = Field(
        default="",
        validation_alias=AliasChoices("STRIPE_PUBLISHABLE_KEY"),
    )
    stripe_webhook_secret: str = Field(
        default="",
        validation_alias=AliasChoices("STRIPE_WEBHOOK_SECRET"),
    )
    stripe_price_id: str = Field(
        default="",
        validation_alias=AliasChoices("STRIPE_PRICE_ID"),
    )
    # Checkout / Portal return URLs (local defaults point at Vite frontend)
    stripe_checkout_success_url: str = Field(
        default="http://localhost:5173/billing?checkout=success",
        validation_alias=AliasChoices("STRIPE_CHECKOUT_SUCCESS_URL"),
    )
    stripe_checkout_cancel_url: str = Field(
        default="http://localhost:5173/billing?checkout=cancel",
        validation_alias=AliasChoices("STRIPE_CHECKOUT_CANCEL_URL"),
    )
    stripe_portal_return_url: str = Field(
        default="http://localhost:5173/billing",
        validation_alias=AliasChoices("STRIPE_PORTAL_RETURN_URL"),
    )
    stripe_metered_price_id: str = Field(
        default="",
        validation_alias=AliasChoices("STRIPE_METERED_PRICE_ID"),
    )
    stripe_meter_event_name: str = Field(
        default="crawl_pages_with_results",
        validation_alias=AliasChoices("STRIPE_METER_EVENT_NAME"),
    )

    # Crawl commerce (public via growth API; Stripe test meter after free tier)
    crawl_free_pages: int = Field(
        default=5000,
        validation_alias=AliasChoices("CRAWL_FREE_PAGES"),
    )
    crawl_price_per_page_cents: int = Field(
        default=2,
        validation_alias=AliasChoices("CRAWL_PRICE_PER_PAGE_CENTS"),
    )
    crawl_price_currency: str = Field(
        default="usd",
        validation_alias=AliasChoices("CRAWL_PRICE_CURRENCY"),
    )

    # Break-glass emergency access (time-boxed admin overrides)
    break_glass_default_ttl_minutes: int = Field(
        default=60,
        validation_alias="BREAK_GLASS_DEFAULT_TTL_MINUTES",
    )
    break_glass_max_ttl_minutes: int = Field(
        default=24 * 60,
        validation_alias="BREAK_GLASS_MAX_TTL_MINUTES",
    )

    # Signed media URLs (S3-minded stub). Prefer a dedicated signing secret in prod.
    media_signing_secret: str = Field(
        default="",
        validation_alias=AliasChoices("MEDIA_SIGNING_SECRET"),
    )
    media_public_base_url: str = Field(
        default="https://media.example.local/objects",
        validation_alias="MEDIA_PUBLIC_BASE_URL",
    )
    media_signed_url_ttl_seconds: int = Field(
        default=300,
        validation_alias="MEDIA_SIGNED_URL_TTL_SECONDS",
    )
    media_signed_url_max_ttl_seconds: int = Field(
        default=3600,
        validation_alias="MEDIA_SIGNED_URL_MAX_TTL_SECONDS",
    )

    # Outbound webhook outbox (HMAC-signed deliveries)
    webhooks_enabled: bool = Field(
        default=True,
        validation_alias="WEBHOOKS_ENABLED",
    )
    webhook_signing_secret: str = Field(
        default="",
        validation_alias=AliasChoices("WEBHOOK_SIGNING_SECRET"),
    )
    webhook_max_attempts: int = Field(
        default=8,
        validation_alias="WEBHOOK_MAX_ATTEMPTS",
    )

    @property
    def cors_origin_list(self) -> list[str]:
        return [origin.strip() for origin in self.cors_origins.split(",") if origin.strip()]

    @property
    def is_production(self) -> bool:
        return self.environment == "prod"

    @property
    def google_configured(self) -> bool:
        return bool(self.google_client_id.strip())

    @property
    def microsoft_configured(self) -> bool:
        return bool(self.microsoft_client_id.strip())


@lru_cache
def get_settings() -> Settings:
    """Cached settings singleton — process env is fixed for the worker lifetime."""
    return Settings()
