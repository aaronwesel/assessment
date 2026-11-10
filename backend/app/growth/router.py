"""
Growth / marketing API — landing pricing and public crawl commerce hints.

UI lives in `frontend/`; values are test-mode placeholders only (no secrets).
"""

from __future__ import annotations

from fastapi import APIRouter

from app.billing import crawl_usage as crawl_usage_service
from app.core.config import get_settings

router = APIRouter(prefix="/growth", tags=["growth"])


@router.get("/status")
def growth_status_stub() -> dict[str, object]:
    return {"status": "ok", "workstream": "C", "crawl_pricing": True}


@router.get("/crawl-pricing")
def crawl_pricing() -> dict[str, object]:
    settings = get_settings()
    base = crawl_usage_service.pricing_snapshot(settings)
    return {
        **base,
        "billable_unit": "pages_with_results",
        "anonymous_crawl_allowed": True,
        "save_jobs_requires_auth": True,
    }
