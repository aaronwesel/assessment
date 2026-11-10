"""
API v1 router aggregation.

Mounted under `settings.api_v1_prefix` (default `/api/v1`) from `create_app`.
Root `/health` and `/ready` stay outside this router for ALB target-group probes.

Workstream A lands platform-ops routers and keeps shared includes stable for
billing (B) and growth (C) mounts on the stacked product-expansion branch.
"""

from fastapi import APIRouter

from app.api.routes import auth, teams
from app.audit.router import router as audit_router
from app.billing import router as billing_router
from app.break_glass.router import router as break_glass_router
from app.consent.router import router as consent_router
from app.flags.router import router as flags_router
from app.growth.router import router as growth_router
from app.health.router import router as health_router
from app.media.router import router as media_router
from app.webhooks.router import router as webhooks_router

api_router = APIRouter()
# Health also under /api/v1 for clients that prefer the versioned prefix.
api_router.include_router(health_router)
api_router.include_router(auth.router)
api_router.include_router(teams.router)

# Workstream A — platform ops
api_router.include_router(audit_router)
api_router.include_router(break_glass_router)
api_router.include_router(consent_router)
api_router.include_router(media_router)
api_router.include_router(flags_router)
api_router.include_router(webhooks_router)

# Workstream B — Stripe billing (real routes; not a stub)
api_router.include_router(billing_router)
# Workstream C — growth mount stub (landing/superadmin live in the frontend)
api_router.include_router(growth_router)
