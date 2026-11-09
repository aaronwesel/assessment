"""Application settings loaded from environment (no secrets in code)."""

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

    database_url: str = "postgresql+psycopg://postgres:postgres@localhost:5432/assessment"
    redis_url: str = "redis://localhost:6379/0"

    # Google Identity Services (One Tap)
    google_client_id: str = ""
    google_client_secret: str = ""

    # Microsoft Entra / MSAL (One Tap)
    microsoft_client_id: str = ""
    microsoft_client_secret: str = ""
    microsoft_tenant_id: str = "common"

    # DeepSeek — OFF by default; never send PHI in prompts
    deepseek_api_key: str = ""
    deepseek_base_url: str = "https://api.deepseek.com"
    deepseek_enabled: bool = False

    # AWS / Rekognition — prefer IAM task role in ECS
    aws_region: str = ""  # set via AWS_REGION env (e.g. your deployment region)
    aws_access_key_id: str = ""
    aws_secret_access_key: str = ""
    rekognition_enabled: bool = False

    @property
    def cors_origin_list(self) -> list[str]:
        return [origin.strip() for origin in self.cors_origins.split(",") if origin.strip()]

    @property
    def is_production(self) -> bool:
        return self.environment == "prod"


@lru_cache
def get_settings() -> Settings:
    return Settings()
