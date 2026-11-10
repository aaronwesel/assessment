"""
Growth / marketing API — landing pricing and public crawl commerce hints.

UI lives in `frontend/`; values are test-mode placeholders only (no secrets).
"""

from __future__ import annotations

import os

from fastapi import APIRouter

router = APIRouter(prefix="/growth", tags=["growth"])


def _price_per_page_cents() -> int:
    raw = os.environ.get("CRAWL_PRICE_PER_PAGE_CENTS", "2")
    try:
        return max(0, int(raw))
    except ValueError:
        return 2


@router.get("/status")
def growth_status_stub() -> dict[str, object]:
    return {"status": "ok", "workstream": "C", "crawl_pricing": True}


@router.get("/crawl-pricing")
def crawl_pricing() -> dict[str, object]:
    cents = _price_per_page_cents()
    currency = os.environ.get("CRAWL_PRICE_CURRENCY", "usd").lower()
    display = f"${cents / 100:.2f}" if currency == "usd" else f"{cents / 100:.2f} {currency.upper()}"
    stripe_metered = bool(os.environ.get("STRIPE_METERED_PRICE_ID", "").strip())
    return {
        "price_per_page_cents": cents,
        "currency": currency,
        "display_per_page": display,
        "stripe_metered_configured": stripe_metered,
        "anonymous_crawl_allowed": True,
        "save_jobs_requires_auth": True,
    }
