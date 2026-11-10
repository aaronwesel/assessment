"""
Per-user crawl page ledger: 5000 free pages-with-results, then Stripe test meter.

Fulfillment for metered line items is driven by Billing Meter events (test mode).
Job rows are idempotent on (user_id, crawl_job_id) so Job Details retries are safe.
"""

from __future__ import annotations

from sqlmodel import Session, func, select

from app.billing import client as stripe_client
from app.billing.models import BillingCustomer, CrawlJobUsage
from app.core.config import Settings, get_settings
from app.core.logging import get_logger, log_event

logger = get_logger(__name__)


class CrawlUsageError(RuntimeError):
    """User-facing crawl billing errors."""


def _display_per_page(cents: int, currency: str) -> str:
    cur = (currency or "usd").lower()
    if cur == "usd":
        return f"${cents / 100:.2f}"
    return f"{cents / 100:.2f} {cur.upper()}"


def pricing_snapshot(settings: Settings | None = None) -> dict[str, object]:
    cfg = settings or get_settings()
    cents = max(0, int(cfg.crawl_price_per_page_cents))
    currency = (cfg.crawl_price_currency or "usd").lower()
    free = max(0, int(cfg.crawl_free_pages))
    metered = bool((cfg.stripe_metered_price_id or "").strip())
    return {
        "price_per_page_cents": cents,
        "currency": currency,
        "display_per_page": _display_per_page(cents, currency),
        "free_pages_allowance": free,
        "stripe_metered_configured": metered,
    }


def total_pages_for_user(session: Session, user_id: str) -> int:
    total = session.exec(
        select(func.coalesce(func.sum(CrawlJobUsage.pages_with_results), 0)).where(
            CrawlJobUsage.user_id == user_id
        )
    ).one()
    return int(total or 0)


def billable_pages(total: int, free_allowance: int) -> int:
    return max(0, total - max(0, free_allowance))


def usage_status_for_user(session: Session, user_id: str, settings: Settings | None = None) -> dict[str, object]:
    cfg = settings or get_settings()
    pricing = pricing_snapshot(cfg)
    total = total_pages_for_user(session, user_id)
    free_allowance = int(pricing["free_pages_allowance"])
    remaining = max(0, free_allowance - total)
    billable = billable_pages(total, free_allowance)
    return {
        **pricing,
        "pages_with_results_total": total,
        "free_pages_remaining": remaining,
        "billable_pages_total": billable,
    }


def _billable_increment(prev_total: int, new_pages: int, free_allowance: int) -> int:
    before = billable_pages(prev_total, free_allowance)
    after = billable_pages(prev_total + new_pages, free_allowance)
    return max(0, after - before)


def record_job_pages(
    session: Session,
    *,
    user_id: str,
    crawl_job_id: str,
    pages_with_results: int,
    settings: Settings | None = None,
) -> dict[str, object]:
    if pages_with_results < 0:
        raise CrawlUsageError("pages_with_results must be non-negative")

    cfg = settings or get_settings()
    job_key = crawl_job_id.strip()
    if not job_key:
        raise CrawlUsageError("crawl_job_id is required")

    existing = session.exec(
        select(CrawlJobUsage).where(
            CrawlJobUsage.user_id == user_id,
            CrawlJobUsage.crawl_job_id == job_key,
        )
    ).first()
    if existing is not None:
        status = usage_status_for_user(session, user_id, cfg)
        return {
            "recorded": False,
            "duplicate": True,
            "pages_with_results": existing.pages_with_results,
            "billable_pages": existing.billable_pages,
            "stripe_meter_events_sent": 0,
            **status,
        }

    prev_total = total_pages_for_user(session, user_id)
    free_allowance = max(0, int(cfg.crawl_free_pages))
    billable = _billable_increment(prev_total, pages_with_results, free_allowance)

    row = CrawlJobUsage(
        user_id=user_id,
        crawl_job_id=job_key,
        pages_with_results=pages_with_results,
        billable_pages=billable,
    )
    session.add(row)
    session.commit()
    session.refresh(row)

    meter_sent = 0
    if billable > 0 and (cfg.stripe_metered_price_id or "").strip():
        customer = session.exec(
            select(BillingCustomer).where(BillingCustomer.user_id == user_id)
        ).first()
        if customer is not None:
            try:
                stripe_client.create_meter_event(
                    event_name=cfg.stripe_meter_event_name,
                    stripe_customer_id=customer.stripe_customer_id,
                    value=billable,
                    idempotency_key=f"crawl-{job_key}",
                )
                meter_sent = billable
                row.stripe_meter_reported = True
                session.add(row)
                session.commit()
            except stripe_client.StripeNotConfiguredError:
                log_event(logger, "crawl_usage_meter_skipped", reason="stripe_not_configured")
            except Exception:  # noqa: BLE001 — Stripe outage must not roll back ledger
                log_event(logger, "crawl_usage_meter_failed", job_id=job_key)
        else:
            log_event(logger, "crawl_usage_meter_skipped", reason="no_stripe_customer")

    status = usage_status_for_user(session, user_id, cfg)
    return {
        "recorded": True,
        "duplicate": False,
        "pages_with_results": pages_with_results,
        "billable_pages": billable,
        "stripe_meter_events_sent": meter_sent,
        **status,
    }
