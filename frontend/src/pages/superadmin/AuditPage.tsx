import { useEffect, useState } from 'react'
import {
  fetchAuditSkim,
  type AuditSkim,
  type FetchResult,
} from '../../api/platform'
import { useAuth } from '../../auth/AuthContext'

export function SuperadminAuditPage() {
  const { tokens } = useAuth()
  const [result, setResult] = useState<FetchResult<AuditSkim> | null>(null)

  useEffect(() => {
    const token = tokens?.access_token
    if (!token) return
    let cancelled = false
    void (async () => {
      const res = await fetchAuditSkim(token)
      if (!cancelled) setResult(res)
    })()
    return () => {
      cancelled = true
    }
  }, [tokens?.access_token])

  const rows =
    result?.available
      ? (result.data.recent ?? result.data.items ?? [])
      : []

  return (
    <main className="sa-page">
      <h1>Audit skim</h1>
      <p className="lede">
        Recent audit events (no PHI). Full CSV/JSON export lives on the API when
        workstream A ships it.
      </p>

      {result === null ? <p className="hint">Loading…</p> : null}
      {result && !result.available ? (
        <p className="hint">
          Audit API unavailable ({result.detail}). Placeholder until{' '}
          <code>/api/v1/audit</code> lands.
        </p>
      ) : null}
      {result?.available ? (
        <>
          {typeof result.data.total === 'number' ? (
            <p className="hint">Total reported: {result.data.total}</p>
          ) : null}
          {rows.length === 0 ? (
            <p className="hint">No recent events in skim payload.</p>
          ) : (
            <pre className="sa-pre">{JSON.stringify(rows, null, 2)}</pre>
          )}
        </>
      ) : null}
    </main>
  )
}
