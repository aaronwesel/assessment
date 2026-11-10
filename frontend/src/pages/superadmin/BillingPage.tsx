import { useEffect, useState } from 'react'
import {
  fetchBillingSkim,
  type BillingSkim,
  type FetchResult,
} from '../../api/platform'
import { useAuth } from '../../auth/AuthContext'

export function SuperadminBillingPage() {
  const { tokens } = useAuth()
  const [result, setResult] = useState<FetchResult<BillingSkim> | null>(null)

  useEffect(() => {
    const token = tokens?.access_token
    if (!token) return
    let cancelled = false
    void (async () => {
      const res = await fetchBillingSkim(token)
      if (!cancelled) setResult(res)
    })()
    return () => {
      cancelled = true
    }
  }, [tokens?.access_token])

  return (
    <main className="sa-page">
      <h1>Billing skim</h1>
      <p className="lede">
        Read-only subscription summary. Checkout / portal live under billing
        settings (workstream B) — this page does not mutate Stripe.
      </p>

      {result === null ? <p className="hint">Loading…</p> : null}
      {result && !result.available ? (
        <p className="hint">
          Billing summary unavailable ({result.detail}). Placeholder until{' '}
          <code>/api/v1/billing/summary</code> lands.
        </p>
      ) : null}
      {result?.available ? (
        <pre className="sa-pre">{JSON.stringify(result.data, null, 2)}</pre>
      ) : null}
    </main>
  )
}
