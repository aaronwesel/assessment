"""Amazon Rekognition client stub via boto3. Prefer IAM task role in ECS."""

from __future__ import annotations

from typing import Any

from app.core.config import Settings, get_settings
from app.core.logging import get_logger

logger = get_logger(__name__)


class RekognitionError(RuntimeError):
    pass


class RekognitionClientStub:
    """Thin wrapper around boto3 Rekognition — stubbed until feature is enabled."""

    def __init__(self, settings: Settings | None = None) -> None:
        self._settings = settings or get_settings()
        self._client: Any | None = None

    @property
    def is_enabled(self) -> bool:
        return bool(self._settings.rekognition_enabled)

    def _get_client(self) -> Any:
        if self._client is not None:
            return self._client
        if not self.is_enabled:
            raise RekognitionError("Rekognition is disabled (REKOGNITION_ENABLED=false)")
        import boto3

        kwargs: dict[str, Any] = {"region_name": self._settings.aws_region}
        # Prefer ECS task role; only pass keys when explicitly provided (local).
        if self._settings.aws_access_key_id and self._settings.aws_secret_access_key:
            kwargs["aws_access_key_id"] = self._settings.aws_access_key_id
            kwargs["aws_secret_access_key"] = self._settings.aws_secret_access_key
        self._client = boto3.client("rekognition", **kwargs)
        return self._client

    def detect_labels(self, image_bytes: bytes, max_labels: int = 10) -> list[dict[str, Any]]:
        """Stub detect_labels — does not send images until explicitly enabled."""
        if not self.is_enabled:
            logger.info("Rekognition detect_labels rejected: feature disabled")
            raise RekognitionError("Rekognition is disabled")
        # HIPAA: never log image bytes or derived PHI.
        logger.info("Rekognition detect_labels stub", extra={"max_labels": max_labels})
        client = self._get_client()
        # Real call deferred; return empty until assessment AI stream enables it.
        _ = client
        return []


def get_rekognition_client(settings: Settings | None = None) -> RekognitionClientStub:
    return RekognitionClientStub(settings)
