"""
Stripe test-mode billing (Checkout, Customer Portal, signed webhooks).

Owns subscription/customer mirrors and the `/api/v1/billing/*` surface.
Never put live secret keys in source; load from env / Secrets Manager.
Webhook fulfillment is the source of truth — not Checkout success redirects.
"""

from app.billing.routes import router

__all__ = ["router"]
