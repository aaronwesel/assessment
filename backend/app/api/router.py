"""
API v1 router aggregation.

Mounted under `settings.api_v1_prefix` (default `/api/v1`) from `create_app`.
Root `/health` stays outside this router for ALB target-group probes.
"""

from fastapi import APIRouter

from app.api.routes import auth, health, teams
from app.billing import router as billing_router

api_router = APIRouter()
api_router.include_router(health.router)
api_router.include_router(auth.router)
api_router.include_router(teams.router)
api_router.include_router(billing_router)
