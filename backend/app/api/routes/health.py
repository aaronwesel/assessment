"""Health and readiness endpoints."""

from fastapi import APIRouter

from app.core.config import get_settings
from app.core.redis import get_redis

router = APIRouter(tags=["health"])


@router.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@router.get("/health/ready")
def readiness() -> dict[str, object]:
    settings = get_settings()
    redis = get_redis()
    return {
        "status": "ready",
        "environment": settings.environment,
        "redis_connected": redis.is_connected,
        "deepseek_enabled": settings.deepseek_enabled,
        "rekognition_enabled": settings.rekognition_enabled,
    }
