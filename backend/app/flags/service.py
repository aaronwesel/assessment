"""
Redis-backed feature flags / kill switches.

Keys: `flag:{name}` → `"1"` (on) or `"0"` (off).
When unset, fall back to Settings defaults so env remains the source of truth
until an operator explicitly overrides.

Kill-switch semantics: an explicit `"0"` disables the feature even if Settings
say enabled. Used for AI, Slack, SES, webhooks, etc. during incidents.
"""

from __future__ import annotations

from typing import Any

from app.core.config import Settings, get_settings
from app.core.logging import get_logger, log_event
from app.core.redis import RedisClientStub, get_redis

logger = get_logger(__name__)

# Canonical flag names used across the scaffold.
KNOWN_FLAGS: dict[str, str] = {
    "ai_deepseek": "DeepSeek LLM calls",
    "ai_rekognition": "Amazon Rekognition calls",
    "slack": "Slack incoming webhook notifications",
    "ses": "Amazon SES outbound mail",
    "webhooks": "Outbound webhook outbox delivery",
    "media_signed_urls": "Signed media URL issuance",
    "break_glass": "Break-glass grant creation",
}


def _key(name: str) -> str:
    return f"flag:{name}"


def _default_enabled(name: str, settings: Settings | None = None) -> bool:
    cfg = settings or get_settings()
    mapping = {
        "ai_deepseek": cfg.deepseek_enabled,
        "ai_rekognition": cfg.rekognition_enabled,
        "slack": cfg.slack_enabled,
        "ses": cfg.ses_enabled,
        "webhooks": cfg.webhooks_enabled,
        "media_signed_urls": True,
        "break_glass": True,
    }
    return bool(mapping.get(name, False))


def get_flag(
    name: str,
    *,
    redis: RedisClientStub | None = None,
    settings: Settings | None = None,
) -> bool:
    """Resolve a flag: explicit Redis override, else Settings default."""
    if name not in KNOWN_FLAGS:
        return False
    client = redis or get_redis()
    raw = client.get(_key(name))
    if raw is None:
        return _default_enabled(name, settings)
    return raw.strip() in {"1", "true", "TRUE", "on", "ON"}


def is_feature_enabled(name: str, *, redis: RedisClientStub | None = None) -> bool:
    """Public alias used by AI gates and alert hooks."""
    return get_flag(name, redis=redis)


def set_flag(
    name: str,
    enabled: bool,
    *,
    redis: RedisClientStub | None = None,
    actor_user_id: str | None = None,
) -> bool:
    """Force a kill-switch / enable override into Redis."""
    if name not in KNOWN_FLAGS:
        raise ValueError(f"unknown flag: {name}")
    client = redis or get_redis()
    client.set(_key(name), "1" if enabled else "0")
    log_event(
        logger,
        "feature_flag_set",
        flag=name,
        enabled=enabled,
        actor_present=bool(actor_user_id),
    )
    return enabled


def clear_flag(name: str, *, redis: RedisClientStub | None = None) -> None:
    """Remove override so Settings default applies again."""
    if name not in KNOWN_FLAGS:
        raise ValueError(f"unknown flag: {name}")
    client = redis or get_redis()
    client.delete(_key(name))
    log_event(logger, "feature_flag_cleared", flag=name)


def list_flags(
    *,
    redis: RedisClientStub | None = None,
    settings: Settings | None = None,
) -> list[dict[str, Any]]:
    client = redis or get_redis()
    cfg = settings or get_settings()
    items: list[dict[str, Any]] = []
    for name, description in sorted(KNOWN_FLAGS.items()):
        raw = client.get(_key(name))
        items.append(
            {
                "name": name,
                "description": description,
                "enabled": get_flag(name, redis=client, settings=cfg),
                "override": None if raw is None else raw.strip() in {"1", "true", "TRUE", "on", "ON"},
                "default": _default_enabled(name, cfg),
            }
        )
    return items
