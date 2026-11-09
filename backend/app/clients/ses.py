"""Amazon SES client via boto3. Feature flag OFF by default; prefer IAM task role in ECS."""

from __future__ import annotations

from typing import Any, Protocol

from app.core.config import Settings, get_settings
from app.core.logging import get_logger

logger = get_logger(__name__)


class SesError(RuntimeError):
    pass


class SesSender(Protocol):
    """Minimal send interface — tests stub this instead of calling AWS."""

    def send_email(
        self,
        *,
        to_addresses: list[str],
        subject: str,
        body_text: str,
        body_html: str | None = None,
    ) -> str:
        """Return a message id (or stub id). Must not log recipient or body content."""
        ...

    def is_enabled(self) -> bool: ...


class DisabledSesClient:
    """Default client when SES_ENABLED is false — never contacts AWS."""

    def is_enabled(self) -> bool:
        return False

    def send_email(
        self,
        *,
        to_addresses: list[str],
        subject: str,
        body_text: str,
        body_html: str | None = None,
    ) -> str:
        # HIPAA: do not log recipients or body (may contain PII/PHI if misused).
        _ = (to_addresses, subject, body_text, body_html)
        logger.info("SES send rejected: feature disabled")
        raise SesError("SES is disabled (SES_ENABLED=false)")


class Boto3SesClient:
    """Thin SES wrapper. Real SendEmail runs only when enabled and from-address is set."""

    def __init__(self, settings: Settings) -> None:
        self._settings = settings
        self._client: Any | None = None

    def is_enabled(self) -> bool:
        return bool(self._settings.ses_enabled and self._settings.ses_from_email)

    def _region(self) -> str:
        return self._settings.ses_region or self._settings.aws_region

    def _get_client(self) -> Any:
        if self._client is not None:
            return self._client
        if not self.is_enabled():
            raise SesError("SES is disabled or SES_FROM_EMAIL missing")
        import boto3

        kwargs: dict[str, Any] = {"region_name": self._region() or None}
        # Prefer ECS task role; only pass keys when explicitly provided (local).
        if self._settings.aws_access_key_id and self._settings.aws_secret_access_key:
            kwargs["aws_access_key_id"] = self._settings.aws_access_key_id
            kwargs["aws_secret_access_key"] = self._settings.aws_secret_access_key
        self._client = boto3.client("ses", **{k: v for k, v in kwargs.items() if v})
        return self._client

    def send_email(
        self,
        *,
        to_addresses: list[str],
        subject: str,
        body_text: str,
        body_html: str | None = None,
    ) -> str:
        if not self.is_enabled():
            raise SesError("SES is disabled or SES_FROM_EMAIL missing")
        if not to_addresses:
            raise SesError("SES send requires at least one recipient")

        # HIPAA: never log recipients, subject, or body.
        logger.info(
            "SES send_email invoked",
            extra={"recipient_count": len(to_addresses), "has_html": body_html is not None},
        )

        body: dict[str, Any] = {"Text": {"Data": body_text, "Charset": "UTF-8"}}
        if body_html is not None:
            body["Html"] = {"Data": body_html, "Charset": "UTF-8"}

        client = self._get_client()
        response = client.send_email(
            Source=self._settings.ses_from_email,
            Destination={"ToAddresses": list(to_addresses)},
            Message={
                "Subject": {"Data": subject, "Charset": "UTF-8"},
                "Body": body,
            },
        )
        message_id = str(response.get("MessageId", ""))
        return message_id


class StubSesClient:
    """In-memory send stub for tests — records calls without contacting AWS."""

    def __init__(self, *, enabled: bool = True) -> None:
        self._enabled = enabled
        self.sent: list[dict[str, Any]] = []

    def is_enabled(self) -> bool:
        return self._enabled

    def send_email(
        self,
        *,
        to_addresses: list[str],
        subject: str,
        body_text: str,
        body_html: str | None = None,
    ) -> str:
        if not self._enabled:
            raise SesError("SES stub is disabled")
        record = {
            "to_addresses": list(to_addresses),
            "subject": subject,
            "body_text": body_text,
            "body_html": body_html,
        }
        self.sent.append(record)
        return f"stub-message-{len(self.sent)}"


def get_ses_client(settings: Settings | None = None) -> SesSender:
    cfg = settings or get_settings()
    if cfg.ses_enabled and cfg.ses_from_email:
        return Boto3SesClient(cfg)
    return DisabledSesClient()
