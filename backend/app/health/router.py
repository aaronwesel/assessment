"""
Health, readiness, and SLO/metrics endpoints.

| Path                 | Role        | Use |
|----------------------|-------------|-----|
| `/health`            | Liveness    | ALB target-group health check |
| `/ready`             | Readiness   | Deploy gates (alias of `/health/ready`) |
| `/health/ready`      | Readiness   | Backward-compatible path |
| `/health/metrics`    | Ops view    | Tiny counters/SLOs for superadmin |
| `/api/v1/health/*`   | Same family | Mounted under API prefix too |

HIPAA: payloads expose booleans and counters only — never secrets or PHI.
"""

from __future__ import annotations

from fastapi import APIRouter, Response

from app.core.config import get_settings
from app.core.redis import get_redis
from app.flags.service import list_flags
from app.health import metrics as health_metrics

router = APIRouter(tags=["health"])


def _readiness_payload(response: Response) -> dict[str, object]:
    settings = get_settings()
    redis = get_redis()

    # In local/test, the in-memory Redis stub is acceptable.
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

    flag_snapshot = {
        item["name"]: item["enabled"] for item in list_flags(redis=redis, settings=settings)
    }

    return {
        "status": "ready" if ready else "not_ready",
        "environment": settings.environment,
        "redis_connected": redis.is_connected,
        "redis_ok": redis_ok,
        "db_ok": db_ok,
        "deepseek_enabled": settings.deepseek_enabled,
        "rekognition_enabled": settings.rekognition_enabled,
        "ses_enabled": settings.ses_enabled,
        "slack_enabled": settings.slack_enabled,
        "webhooks_enabled": settings.webhooks_enabled,
        "feature_flags": flag_snapshot,
    }


@router.get("/health")
def health() -> dict[str, str]:
    """Process liveness — suitable for ALB/ECS health checks."""
    return {"status": "ok"}


@router.get("/health/ready")
def readiness(response: Response) -> dict[str, object]:
    """Dependency readiness — 200 when ready, 503 when critical deps are down."""
    return _readiness_payload(response)


@router.get("/ready")
def ready_alias(response: Response) -> dict[str, object]:
    """Alias of `/health/ready` for ops tooling that expects `/ready`."""
    return _readiness_payload(response)


@router.get("/health/metrics")
def health_metrics_view() -> dict[str, object]:
    """Tiny ops metrics / SLO snapshot (non-PHI labels only)."""
    return {"status": "ok", "metrics": health_metrics.snapshot()}


@router.get("/health/slo")
def health_slo_view() -> dict[str, object]:
    """SLO-focused subset for a superadmin dashboard widget."""
    snap = health_metrics.snapshot()
    return {
        "status": "ok",
        "slo": snap["slo"],
        "availability": snap["availability"],
        "latency_ms_p95": snap["latency_ms_p95"],
        "requests_total": snap["requests_total"],
        "errors_5xx": snap["errors_5xx"],
    }
