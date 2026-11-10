import { useEffect, useState } from 'react'
import {
  downloadAuditExport,
  fetchAuditSkim,
  type AuditSkim,
  type FetchResult,
} from '../../api/platform'
import { useAuth } from '../../auth/AuthContext'

export function SuperadminAuditPage() {
  const { tokens } = useAuth()
  const [result, setResult] = useState<FetchResult<AuditSkim> | null>(null)
  const [exportError, setExportError] = useState<string | null>(null)
  const [exportBusy, setExportBusy] = useState(false)

  useEffect(() => {
    const token = tokens?.access_token
    if (!token) return
    let cancelled = false
    void (async () => {
      const res = await fetchAuditSkim(token, 20)
      if (!cancelled) setResult(res)
    })()
    return () => {
      cancelled = true
    }
  }, [tokens?.access_token])

  const rows = result?.available ? (result.data.items ?? result.data.recent ?? []) : []

  async function onExport(format: 'csv' | 'json') {
    const token = tokens?.access_token
    if (!token) return
    setExportBusy(true)
    setExportError(null)
    const res = await downloadAuditExport(token, format)
    if (!res.ok) setExportError(res.detail)
    setExportBusy(false)
  }

  return (
    <main className="sa-page">
      <h1>Audit skim</h1>
      <p className="lede">
        Recent events from <code>/api/v1/audit/events</code> (opaque ids only). Export
        uses <code>/api/v1/audit/export</code>.
      </p>

      <div className="sa-actions">
        <button
          type="button"
          className="btn btn-ghost"
          disabled={exportBusy || !tokens?.access_token}
          onClick={() => {
            void onExport('csv')
          }}
        >
          Export CSV
        </button>
        <button
          type="button"
          className="btn btn-ghost"
          disabled={exportBusy || !tokens?.access_token}
          onClick={() => {
            void onExport('json')
          }}
        >
          Export JSON
        </button>
      </div>
      {exportError ? <p className="form-error">{exportError}</p> : null}

      {result === null ? <p className="hint">Loading…</p> : null}
      {result && !result.available ? (
        <p className="hint">
          Audit API unavailable ({result.detail}). Expect{' '}
          <code>/api/v1/audit/events</code>.
        </p>
      ) : null}
      {result?.available ? (
        rows.length === 0 ? (
          <p className="hint">No recent events.</p>
        ) : (
          <div className="sa-table-wrap">
            <table className="sa-table">
              <thead>
                <tr>
                  <th>When</th>
                  <th>Action</th>
                  <th>Resource</th>
                  <th>Actor</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={String(row.id ?? `${row.action}-${row.created_at}`)}>
                    <td>{row.created_at ?? '—'}</td>
                    <td>
                      <code>{row.action ?? '—'}</code>
                    </td>
                    <td>
                      <code>
                        {row.resource_type ?? '—'}
                        {row.resource_id ? `:${row.resource_id}` : ''}
                      </code>
                    </td>
                    <td>
                      <code>{row.actor_user_id ?? '—'}</code>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )
      ) : null}
    </main>
  )
}
