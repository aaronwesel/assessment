/**
 * Authenticated billing settings — status mirror + Checkout / Portal entry points.
 *
 * Success redirects are informational only; subscription state updates via
 * signed Stripe webhooks on the API.
 */
import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import {
  createCheckoutSession,
  createPortalSession,
  fetchBillingConfig,
  fetchBillingStatus,
  fetchCrawlUsage,
  type BillingConfig,
  type BillingStatus,
  type CrawlUsageStatus,
} from '../../api/billing'
import { BillingActions } from '../../components/billing/BillingActions'

export function BillingSettingsPage() {
  const { tokens } = useAuth()
  const [searchParams] = useSearchParams()
  const [config, setConfig] = useState<BillingConfig | null>(null)
  const [status, setStatus] = useState<BillingStatus | null>(null)
  const [crawlUsage, setCrawlUsage] = useState<CrawlUsageStatus | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const checkoutHint = searchParams.get('checkout')

  useEffect(() => {
    let cancelled = false
    async function load() {
      if (!tokens?.access_token) return
      try {
        const [cfg, st, usage] = await Promise.all([
          fetchBillingConfig(),
          fetchBillingStatus(tokens.access_token),
          fetchCrawlUsage(tokens.access_token),
        ])
        if (!cancelled) {
          setConfig(cfg)
          setStatus(st)
          setCrawlUsage(usage)
          setError(null)
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Failed to load billing')
        }
      }
    }
    void load()
    return () => {
      cancelled = true
    }
  }, [tokens?.access_token])

  async function onCheckout() {
    if (!tokens?.access_token) return
    setBusy(true)
    setError(null)
    try {
      const session = await createCheckoutSession(tokens.access_token)
      window.location.assign(session.url)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Checkout failed')
      setBusy(false)
    }
  }

  async function onPortal() {
    if (!tokens?.access_token) return
    setBusy(true)
    setError(null)
    try {
      const session = await createPortalSession(tokens.access_token)
      window.location.assign(session.url)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Portal failed')
      setBusy(false)
    }
  }

  const active =
    status?.subscription_status === 'active' || status?.subscription_status === 'trialing'

  return (
    <main className="page billing-page">
      <h1>Billing</h1>
      <p className="lede">
        Manage your subscription via Stripe Checkout and the Customer Portal (test mode). Crawl billing counts
        pages-with-results only — {(crawlUsage?.free_pages_allowance ?? 5000).toLocaleString()} free, then{' '}
        {crawlUsage?.display_per_page ?? 'the published rate'}.
      </p>

      {checkoutHint === 'success' ? (
        <p className="billing-banner billing-banner-ok">
          Checkout completed. Entitlement updates when the Stripe webhook is processed — refresh
          shortly if status still shows pending.
        </p>
      ) : null}
      {checkoutHint === 'cancel' ? (
        <p className="billing-banner">Checkout canceled. No charge was made.</p>
      ) : null}

      {error ? <p className="form-error">{error}</p> : null}

      <dl className="home-meta billing-meta">
        <div>
          <dt>Stripe configured</dt>
          <dd>{config?.configured ? 'Yes' : 'No (set env placeholders)'}</dd>
        </div>
        <div>
          <dt>Status</dt>
          <dd>{status?.subscription_status ?? '—'}</dd>
        </div>
        <div>
          <dt>Cancel at period end</dt>
          <dd>{status?.cancel_at_period_end ? 'Yes' : 'No'}</dd>
        </div>
        <div>
          <dt>Period end</dt>
          <dd>
            {status?.current_period_end
              ? new Date(status.current_period_end).toLocaleString()
              : '—'}
          </dd>
        </div>
        <div>
          <dt>Crawl meter (test)</dt>
          <dd>
            {crawlUsage
              ? `${crawlUsage.pages_with_results_total.toLocaleString()} used · ${crawlUsage.free_pages_remaining.toLocaleString()} free remaining`
              : '—'}
          </dd>
        </div>
        <div>
          <dt>Billable pages</dt>
          <dd>{crawlUsage ? crawlUsage.billable_pages_total.toLocaleString() : '—'}</dd>
        </div>
        <div>
          <dt>Stripe meter configured</dt>
          <dd>{config?.metered_configured ? 'Yes' : 'No'}</dd>
        </div>
      </dl>

      <BillingActions
        busy={busy}
        canCheckout={Boolean(config?.configured) && !active}
        canPortal={Boolean(status?.has_customer)}
        onCheckout={() => {
          void onCheckout()
        }}
        onPortal={() => {
          void onPortal()
        }}
      />

      <p className="hint">
        Secrets stay on the API (`STRIPE_SECRET_KEY`, `STRIPE_WEBHOOK_SECRET`). See{' '}
        <code>docs/billing-stripe.md</code> for Stripe CLI smoke steps.
      </p>
    </main>
  )
}
