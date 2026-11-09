"""Health and readiness endpoints for load balancers and orchestrators.

| Path            | Role        | Use |
|-----------------|-------------|-----|
| `/health`       | Liveness    | ALB target-group health check — cheap, no deps |
| `/health/ready` | Readiness   | Deploy gates / ops — checks Redis (+ optional DB) |

ALB target groups should probe `/health` only. Returning 5xx from readiness
must not be the ALB health path or tasks would flap when Redis briefly blips.
Workers are not behind the ALB; they do not need these routes for fan-out.
"""

from __future__ import annotations

from fastapi import APIRouter, Response

from app.core.config import get_settings
from app.core.redis import get_redis

router = APIRouter(tags=["health"])


@router.get("/health")
def health() -> dict[str, str]:
    """Process liveness — suitable for ALB/ECS health checks."""
    return {"status": "ok"}


@router.get("/health/ready")
def readiness(response: Response) -> dict[str, object]:
    """Dependency readiness — 200 when ready, 503 when critical deps are down."""
    settings = get_settings()
    redis = get_redis()

    # In local/test, the in-memory Redis stub is acceptable ("connected" false
    # still serves sessions). In AWS, prefer a live ElastiCache ping.
    redis_ok = redis.is_connected or settings.environment in {"local", "test"}

    db_ok = True
    if settings.environment not in {"local", "test"}:
        try:
            from sqlalchemy import text

            from app.db.session import SessionLocal

            with SessionLocal() as session:
                session.execute(text("SELECT 1"))
        except Exception:
            db_ok = False

    ready = redis_ok and db_ok
    if not ready:
        response.status_code = 503

    return {
        "status": "ready" if ready else "not_ready",
        "environment": settings.environment,
        "redis_connected": redis.is_connected,
        "redis_ok": redis_ok,
        "db_ok": db_ok,
        "deepseek_enabled": settings.deepseek_enabled,
        "rekognition_enabled": settings.rekognition_enabled,
        "ses_enabled": settings.ses_enabled,
    }
