"""
Billing HTTP surface under `/api/v1/billing/*`.

Webhook path is `/billing/webhooks/stripe` (not `/webhooks`) so it does not
collide with the platform outbox module.
"""

from __future__ import annotations

from fastapi import APIRouter, Header, HTTPException, Request, status

from sqlmodel import func, select

from app.api.deps import CurrentAuth, RequireAdmin, SessionDep
from app.billing import client as stripe_client
from app.billing import crawl_usage as crawl_usage_service
from app.billing import service as billing_service
from app.billing import webhooks as webhook_service
from app.billing.models import BillingCustomer, BillingSubscription, SubscriptionStatus
from app.billing.schemas import (
    BillingConfigResponse,
    BillingStatusResponse,
    BillingSummaryResponse,
    CheckoutSessionRequest,
    CheckoutSessionResponse,
    CrawlUsageStatusResponse,
    PortalSessionRequest,
    PortalSessionResponse,
    RecordCrawlUsageRequest,
    RecordCrawlUsageResponse,
    WebhookAckResponse,
)
from app.core.config import get_settings
from app.core.logging import get_logger, log_event
from app.models.user import User

router = APIRouter(prefix="/billing", tags=["billing"])
logger = get_logger(__name__)


@router.get(
    "/config",
    response_model=BillingConfigResponse,
    summary="Public Stripe publishable config (no secrets)",
)
def billing_config() -> BillingConfigResponse:
    settings = get_settings()
    publishable = (settings.stripe_publishable_key or "").strip()
    return BillingConfigResponse(
        configured=billing_service.billing_configured(settings),
        publishable_key=publishable,
        price_configured=bool((settings.stripe_price_id or "").strip()),
        metered_configured=stripe_client.metered_billing_configured(settings),
    )


@router.get(
    "/status",
    response_model=BillingStatusResponse,
    summary="Current user's subscription mirror",
)
def billing_status(auth: CurrentAuth, session: SessionDep) -> BillingStatusResponse:
    data = billing_service.status_for_user(session, auth.subject)
    return BillingStatusResponse(**data)


@router.get(
    "/me",
    response_model=BillingStatusResponse,
    summary="Alias of /status (superadmin skim fallback)",
)
def billing_me(auth: CurrentAuth, session: SessionDep) -> BillingStatusResponse:
    return billing_status(auth, session)


@router.get(
    "/summary",
    response_model=BillingSummaryResponse,
    summary="Admin billing skim (counts + caller mirror)",
)
def billing_summary(auth: RequireAdmin, session: SessionDep) -> BillingSummaryResponse:
    settings = get_settings()
    customer_count = session.exec(select(func.count()).select_from(BillingCustomer)).one()
    active_subscription_count = session.exec(
        select(func.count())
        .select_from(BillingSubscription)
        .where(
            BillingSubscription.status.in_(
                [
                    SubscriptionStatus.ACTIVE.value,
                    SubscriptionStatus.TRIALING.value,
                ]
            )
        )
    ).one()
    mine = billing_service.status_for_user(session, auth.subject)
    return BillingSummaryResponse(
        status="ok",
        configured=billing_service.billing_configured(settings),
        customer_count=int(customer_count or 0),
        active_subscription_count=int(active_subscription_count or 0),
        subscription_status=mine["subscription_status"],
        customer_id=mine["stripe_customer_id"],
        plan=mine["stripe_price_id"],
    )


@router.get(
    "/crawl-usage",
    response_model=CrawlUsageStatusResponse,
    summary="Crawl pages-with-results meter (free tier + totals)",
)
def crawl_usage_status(auth: CurrentAuth, session: SessionDep) -> CrawlUsageStatusResponse:
    data = crawl_usage_service.usage_status_for_user(session, auth.subject)
    return CrawlUsageStatusResponse(**data)


@router.post(
    "/crawl-usage/record",
    response_model=RecordCrawlUsageResponse,
    summary="Record billable crawl pages-with-results for a completed job",
)
def record_crawl_usage(
    body: RecordCrawlUsageRequest,
    auth: CurrentAuth,
    session: SessionDep,
) -> RecordCrawlUsageResponse:
    try:
        data = crawl_usage_service.record_job_pages(
            session,
            user_id=auth.subject,
            crawl_job_id=body.crawl_job_id,
            pages_with_results=body.pages_with_results,
        )
    except crawl_usage_service.CrawlUsageError as exc:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(exc)) from exc
    return RecordCrawlUsageResponse(**data)


@router.post(
    "/checkout-session",
    response_model=CheckoutSessionResponse,
    summary="Create Stripe Checkout Session (subscription)",
)
def create_checkout_session(
    body: CheckoutSessionRequest,
    auth: CurrentAuth,
    session: SessionDep,
) -> CheckoutSessionResponse:
    # Ensure the auth subject still exists (JWT may outlive a deleted user in tests).
    if session.get(User, auth.subject) is None:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="User not found")
    try:
        result = billing_service.create_checkout_session_for_user(
            session,
            user_id=auth.subject,
            success_url=body.success_url,
            cancel_url=body.cancel_url,
        )
    except stripe_client.StripeNotConfiguredError as exc:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Stripe is not configured",
        ) from exc
    except billing_service.BillingServiceError as exc:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(exc)) from exc
    except Exception as exc:  # noqa: BLE001
        log_event(logger, "billing_checkout_failed")
        raise HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail="Failed to create Checkout Session",
        ) from exc
    return CheckoutSessionResponse(**result)


@router.post(
    "/portal-session",
    response_model=PortalSessionResponse,
    summary="Create Stripe Customer Portal session",
)
def create_portal_session(
    body: PortalSessionRequest,
    auth: CurrentAuth,
    session: SessionDep,
) -> PortalSessionResponse:
    try:
        result = billing_service.create_portal_session_for_user(
            session,
            user_id=auth.subject,
            return_url=body.return_url,
        )
    except stripe_client.StripeNotConfiguredError as exc:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Stripe is not configured",
        ) from exc
    except billing_service.BillingServiceError as exc:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(exc)) from exc
    except Exception as exc:  # noqa: BLE001
        log_event(logger, "billing_portal_failed")
        raise HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail="Failed to create Customer Portal session",
        ) from exc
    return PortalSessionResponse(**result)


@router.post(
    "/webhooks/stripe",
    response_model=WebhookAckResponse,
    summary="Stripe webhook (signature verified, idempotent)",
    include_in_schema=False,
)
async def stripe_webhook(
    request: Request,
    session: SessionDep,
    stripe_signature: str | None = Header(default=None, alias="Stripe-Signature"),
) -> WebhookAckResponse:
    settings = get_settings()
    try:
        webhook_secret = stripe_client.require_webhook_secret(settings)
    except stripe_client.StripeNotConfiguredError as exc:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Stripe webhook secret is not configured",
        ) from exc

    payload = await request.body()
    try:
        event = webhook_service.verify_and_parse_event(payload, stripe_signature, webhook_secret)
    except webhook_service.WebhookVerificationError as exc:
        log_event(logger, "billing_webhook_rejected")
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(exc)) from exc

    event_id = event["id"] if isinstance(event, dict) else event.id
    event_type = event["type"] if isinstance(event, dict) else event.type

    should_process = webhook_service.record_event_once(
        session, event_id=str(event_id), event_type=str(event_type)
    )
    if not should_process:
        log_event(logger, "billing_webhook_duplicate", event_type=str(event_type))
        return WebhookAckResponse(received=True, duplicate=True, event_type=str(event_type))

    try:
        detail = webhook_service.process_stripe_event(session, event)
        webhook_service.mark_event_processed(session, str(event_id), detail=detail)
    except Exception as exc:  # noqa: BLE001
        # Return 500 so Stripe retries; leave ledger row for ops inspection.
        log_event(logger, "billing_webhook_handler_error", event_type=str(event_type))
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Webhook handler failed",
        ) from exc

    return WebhookAckResponse(received=True, duplicate=False, event_type=str(event_type))
