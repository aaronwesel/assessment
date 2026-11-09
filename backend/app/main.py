"""FastAPI application factory."""

from collections.abc import AsyncIterator
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.api.router import api_router
from app.api.routes import health
from app.core.config import get_settings
from app.core.logging import get_logger, log_event
from app.core.redis import get_redis
from app.middleware.rate_limit import RateLimitMiddleware
from app.middleware.request_id import RequestIdMiddleware
from app.middleware.security_headers import SecurityHeadersMiddleware

logger = get_logger(__name__)


@asynccontextmanager
async def lifespan(_app: FastAPI) -> AsyncIterator[None]:
    settings = get_settings()
    log_event(
        logger,
        "app_startup",
        environment=settings.environment,
        deepseek_enabled=settings.deepseek_enabled,
        rekognition_enabled=settings.rekognition_enabled,
        google_configured=settings.google_configured,
        microsoft_configured=settings.microsoft_configured,
        ses_enabled=settings.ses_enabled,
        slack_enabled=settings.slack_enabled,
    )
    get_redis()
    # Local/test: ensure schema without requiring a prior alembic upgrade.
    # Production should run migrations as a deploy step (not on API boot).
    if settings.environment in {"local", "test"}:
        from app.db.session import init_db

        init_db()
    yield
    log_event(logger, "app_shutdown")


def create_app() -> FastAPI:
    settings = get_settings()
    application = FastAPI(
        title=settings.app_name,
        lifespan=lifespan,
        docs_url="/docs" if not settings.is_production else None,
        redoc_url=None,
    )
    application.add_middleware(
        CORSMiddleware,
        allow_origins=settings.cors_origin_list,
        allow_credentials=True,
        allow_methods=["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"],
        allow_headers=["Authorization", "Content-Type", "X-Request-ID"],
        expose_headers=["X-Request-ID"],
    )
    # Order: last added runs first on request. Request ID wraps everything.
    application.add_middleware(SecurityHeadersMiddleware)
    application.add_middleware(RateLimitMiddleware)
    application.add_middleware(RequestIdMiddleware)
    # Root health for ALB/ECS probes (/health liveness, /health/ready deps).
    application.include_router(health.router)
    application.include_router(api_router, prefix=settings.api_v1_prefix)
    return application


app = create_app()
