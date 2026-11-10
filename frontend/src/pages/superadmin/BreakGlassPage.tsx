import { useEffect, useState } from 'react'
import {
  fetchBreakGlassGrants,
  type BreakGlassGrant,
  type FetchResult,
} from '../../api/platform'
import { useAuth } from '../../auth/AuthContext'

export function SuperadminBreakGlassPage() {
  const { tokens } = useAuth()
  const [result, setResult] = useState<FetchResult<BreakGlassGrant[]> | null>(null)

  useEffect(() => {
    const token = tokens?.access_token
    if (!token) return
    let cancelled = false
    void (async () => {
      const res = await fetchBreakGlassGrants(token, true)
      if (!cancelled) setResult(res)
    })()
    return () => {
      cancelled = true
    }
  }, [tokens?.access_token])

  return (
    <main className="sa-page">
      <h1>Break-glass</h1>
      <p className="lede">
        Active emergency grants from <code>/api/v1/break-glass/grants</code>. Creation
        stays on the API / ops runbook — this view is a skim only.
      </p>

      {result === null ? <p className="hint">Loading…</p> : null}
      {result && !result.available ? (
        <p className="hint">Break-glass API unavailable ({result.detail}).</p>
      ) : null}
      {result?.available && result.data.length === 0 ? (
        <p className="hint">No active grants.</p>
      ) : null}
      {result?.available && result.data.length > 0 ? (
        <div className="sa-table-wrap">
          <table className="sa-table">
            <thead>
              <tr>
                <th>Reason</th>
                <th>Target</th>
                <th>Expires</th>
                <th>Ticket</th>
              </tr>
            </thead>
            <tbody>
              {result.data.map((grant) => (
                <tr key={grant.id}>
                  <td>
                    <code>{grant.reason_code}</code>
                  </td>
                  <td>
                    <code>{grant.target_user_id}</code>
                  </td>
                  <td>{grant.expires_at}</td>
                  <td>{grant.ticket_ref ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </main>
  )
}
