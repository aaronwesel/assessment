import { useEffect, useState } from 'react'
import {
  fetchBillingSkim,
  type BillingSkim,
  type FetchResult,
} from '../../api/platform'
import { useAuth } from '../../auth/AuthContext'
import { Link } from 'react-router-dom'

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
        Admin summary from <code>/api/v1/billing/summary</code>. Checkout / portal
        live under <Link to="/billing">Billing settings</Link> — this page does not
        mutate Stripe.
      </p>

      {result === null ? <p className="hint">Loading…</p> : null}
      {result && !result.available ? (
        <p className="hint">
          Billing summary unavailable ({result.detail}). Expect{' '}
          <code>/api/v1/billing/summary</code>.
        </p>
      ) : null}
      {result?.available ? (
        <pre className="sa-pre">{JSON.stringify(result.data, null, 2)}</pre>
      ) : null}
    </main>
  )
}
