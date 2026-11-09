"""DeepSeek client interface. Feature flag OFF by default; never send PHI in prompts."""

from __future__ import annotations

from abc import ABC, abstractmethod
from typing import Any

from app.core.config import Settings, get_settings
from app.core.logging import get_logger

logger = get_logger(__name__)


class DeepSeekError(RuntimeError):
    pass


class DeepSeekClient(ABC):
    """Interface for DeepSeek LLM calls — keep prompts free of PHI."""

    @abstractmethod
    def complete(self, prompt: str, *, model: str = "deepseek-chat") -> str:
        raise NotImplementedError

    @abstractmethod
    def is_enabled(self) -> bool:
        raise NotImplementedError


class DisabledDeepSeekClient(DeepSeekClient):
    """Default client when DEEPSEEK_ENABLED is false."""

    def is_enabled(self) -> bool:
        return False

    def complete(self, prompt: str, *, model: str = "deepseek-chat") -> str:
        # Do not log the prompt (may contain user content / PHI).
        logger.info("DeepSeek call rejected: feature disabled", extra={"model": model})
        raise DeepSeekError("DeepSeek is disabled (DEEPSEEK_ENABLED=false)")


class HttpDeepSeekClient(DeepSeekClient):
    """Stub HTTP client — does not call the network until enabled and wired."""

    def __init__(self, settings: Settings) -> None:
        self._settings = settings

    def is_enabled(self) -> bool:
        return bool(self._settings.deepseek_enabled and self._settings.deepseek_api_key)

    def complete(self, prompt: str, *, model: str = "deepseek-chat") -> str:
        if not self.is_enabled():
            raise DeepSeekError("DeepSeek is disabled or API key missing")
        # Placeholder: real httpx call lands when assessment AI stream enables it.
        # HIPAA: never log prompt content.
        logger.info("DeepSeek complete stub invoked", extra={"model": model})
        return ""


def get_deepseek_client(settings: Settings | None = None) -> DeepSeekClient:
    cfg = settings or get_settings()
    if cfg.deepseek_enabled and cfg.deepseek_api_key:
        return HttpDeepSeekClient(cfg)
    return DisabledDeepSeekClient()
