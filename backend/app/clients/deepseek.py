"""
DeepSeek client interface. Feature flag OFF by default; never send PHI in prompts.

Why a stub + disabled default: AI calls are a HIPAA risk surface (prompts can
leak patient content to a third party). Keep `DEEPSEEK_ENABLED=false` until
assessment AI flows define allowlisted, de-identified prompt shapes.

Consent: callers that process subject data must also pass `require_deepseek_consent`
(see `app.consent.gate`) before invoking `complete`. The gated helper
`complete_for_subject` enforces flag + consent in one place.

Do not log prompt text even when the feature is enabled.
"""

from __future__ import annotations

from abc import ABC, abstractmethod
from typing import Any

from sqlmodel import Session

from app.consent.gate import ConsentRequired, require_deepseek_consent
from app.core.config import Settings, get_settings
from app.core.logging import get_logger
from app.flags.service import is_feature_enabled

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
        _ = prompt
        logger.info("DeepSeek call rejected: feature disabled", extra={"model": model})
        raise DeepSeekError("DeepSeek is disabled (DEEPSEEK_ENABLED=false)")


class HttpDeepSeekClient(DeepSeekClient):
    """Stub HTTP client — does not call the network until enabled and wired."""

    def __init__(self, settings: Settings) -> None:
        self._settings = settings

    def is_enabled(self) -> bool:
        return bool(
            self._settings.deepseek_enabled
            and self._settings.deepseek_api_key
            and is_feature_enabled("ai_deepseek")
        )

    def complete(self, prompt: str, *, model: str = "deepseek-chat") -> str:
        if not self.is_enabled():
            raise DeepSeekError("DeepSeek is disabled, kill-switched, or API key missing")
        # Placeholder: real httpx call lands when assessment AI stream enables it.
        # HIPAA: never log prompt content.
        _ = prompt
        logger.info("DeepSeek complete stub invoked", extra={"model": model})
        return ""


def get_deepseek_client(settings: Settings | None = None) -> DeepSeekClient:
    cfg = settings or get_settings()
    if cfg.deepseek_enabled and cfg.deepseek_api_key and is_feature_enabled("ai_deepseek"):
        return HttpDeepSeekClient(cfg)
    return DisabledDeepSeekClient()


def complete_for_subject(
    session: Session,
    *,
    subject_user_id: str,
    prompt: str,
    model: str = "deepseek-chat",
    settings: Settings | None = None,
) -> str:
    """
    Consent-gated DeepSeek completion.

    Raises ConsentRequired when the subject has not granted `ai_deepseek`,
    or DeepSeekError when the feature/kill-switch blocks the call.
    """
    require_deepseek_consent(session, subject_user_id=subject_user_id)
    client = get_deepseek_client(settings)
    return client.complete(prompt, model=model)


__all__ = [
    "ConsentRequired",
    "DeepSeekClient",
    "DeepSeekError",
    "DisabledDeepSeekClient",
    "HttpDeepSeekClient",
    "complete_for_subject",
    "get_deepseek_client",
]
