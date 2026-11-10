# Stripe billing (test mode)

Checkout Session (subscription) + Customer Portal + signed webhooks. Secrets stay in env / Secrets Manager — never commit real keys.

## Env placeholders

| Variable | Where | Notes |
|----------|--------|--------|
| `STRIPE_SECRET_KEY` | API | Prefer restricted key `rk_test_...` over `sk_test_...` |
| `STRIPE_PUBLISHABLE_KEY` | API (+ optional `VITE_STRIPE_PUBLISHABLE_KEY`) | `pk_test_...` only |
| `STRIPE_WEBHOOK_SECRET` | API | `whsec_...` from Dashboard or Stripe CLI |
| `STRIPE_PRICE_ID` | API | Test-mode Price id (`price_...`) |
| `STRIPE_CHECKOUT_SUCCESS_URL` | API | Default `http://localhost:5173/billing?checkout=success` |
| `STRIPE_CHECKOUT_CANCEL_URL` | API | Default `http://localhost:5173/billing?checkout=cancel` |
| `STRIPE_PORTAL_RETURN_URL` | API | Default `http://localhost:5173/billing` |

Copy from `.env.example` / `backend/.env.example`. Use a [Stripe sandbox](https://docs.stripe.com/sandboxes) for local/CI isolation when possible.

## API surface

| Method | Path | Auth |
|--------|------|------|
| `GET` | `/api/v1/billing/config` | Public (publishable key + configured flags) |
| `GET` | `/api/v1/billing/status` | JWT |
| `GET` | `/api/v1/billing/me` | JWT (alias of `/status`) |
| `GET` | `/api/v1/billing/summary` | Admin JWT (superadmin skim) |
| `POST` | `/api/v1/billing/checkout-session` | JWT |
| `POST` | `/api/v1/billing/portal-session` | JWT |
| `POST` | `/api/v1/billing/webhooks/stripe` | Stripe-Signature |

UI: `/billing` (React under `frontend/src/pages/billing/` and `frontend/src/components/billing/`).

## Smoke with Stripe CLI

1. Create a test Product + recurring Price in the Dashboard (or CLI); set `STRIPE_PRICE_ID`.
2. Put a test secret/RAK and publishable key in local env; run API + frontend.
3. Forward webhooks:

   ```bash
   stripe listen --forward-to localhost:8000/api/v1/billing/webhooks/stripe
   ```

4. Copy the CLI `whsec_...` into `STRIPE_WEBHOOK_SECRET`, restart the API.
5. Sign in → **Billing** → **Subscribe with Stripe Checkout** → pay with test card `4242 4242 4242 4242`.
6. Confirm webhook deliveries (`checkout.session.completed`, `customer.subscription.*`) and that `GET /api/v1/billing/status` shows `active` / `trialing`.
7. **Open Customer Portal** to update payment method or cancel (configure portal in Dashboard → Settings → Billing → Customer portal).

## Safety notes

- Webhook signature verification is required; handlers are idempotent on `event.id`.
- Fulfillment is webhook-driven — do not grant entitlement from the success URL alone.
- No `payment_method_types` hardcoding (dynamic payment methods).
- Path is under `/billing/webhooks/stripe` so it does not collide with the platform webhook outbox.
- Tax: if charging US/EU customers in production, enable [Stripe Tax](https://docs.stripe.com/billing/taxes/collect-taxes) and complete registrations before turning on `automatic_tax`.
