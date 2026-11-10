/**
 * Thin fetch client for `/api/v1/billing/*`.
 *
 * Checkout + Customer Portal URLs come from the API (secret key stays server-side).
 * Do not log Stripe customer/subscription ids alongside emails.
 */
import { config } from '../config'

const jsonHeaders = { 'Content-Type': 'application/json' }

function apiUrl(path: string): string {
  const base = config.apiBaseUrl.replace(/\/$/, '')
  return `${base}${path}`
}

async function readErrorMessage(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as { detail?: string }
    if (typeof body.detail === 'string') return body.detail
  } catch {
    /* ignore */
  }
  return `Request failed (${response.status})`
}

export type BillingConfig = {
  configured: boolean
  publishable_key: string
  price_configured: boolean
}

export type BillingStatus = {
  has_customer: boolean
  stripe_customer_id: string | null
  subscription_status: string
  stripe_subscription_id: string | null
  stripe_price_id: string | null
  cancel_at_period_end: boolean
  current_period_end: string | null
}

export async function fetchBillingConfig(): Promise<BillingConfig> {
  const response = await fetch(apiUrl('/api/v1/billing/config'))
  if (!response.ok) throw new Error(await readErrorMessage(response))
  return response.json() as Promise<BillingConfig>
}

export async function fetchBillingStatus(accessToken: string): Promise<BillingStatus> {
  const response = await fetch(apiUrl('/api/v1/billing/status'), {
    headers: { Authorization: `Bearer ${accessToken}` },
  })
  if (!response.ok) throw new Error(await readErrorMessage(response))
  return response.json() as Promise<BillingStatus>
}

export async function createCheckoutSession(
  accessToken: string,
): Promise<{ session_id: string; url: string }> {
  const response = await fetch(apiUrl('/api/v1/billing/checkout-session'), {
    method: 'POST',
    headers: { Authorization: `Bearer ${accessToken}`, ...jsonHeaders },
    body: JSON.stringify({}),
  })
  if (!response.ok) throw new Error(await readErrorMessage(response))
  return response.json() as Promise<{ session_id: string; url: string }>
}

export async function createPortalSession(
  accessToken: string,
): Promise<{ url: string }> {
  const response = await fetch(apiUrl('/api/v1/billing/portal-session'), {
    method: 'POST',
    headers: { Authorization: `Bearer ${accessToken}`, ...jsonHeaders },
    body: JSON.stringify({}),
  })
  if (!response.ok) throw new Error(await readErrorMessage(response))
  return response.json() as Promise<{ url: string }>
}
