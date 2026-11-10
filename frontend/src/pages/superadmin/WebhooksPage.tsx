import { useEffect, useState } from 'react'
import {
  fetchWebhookOutbox,
  type FetchResult,
  type WebhookOutboxRow,
} from '../../api/platform'
import { useAuth } from '../../auth/AuthContext'

export function SuperadminWebhooksPage() {
  const { tokens } = useAuth()
  const [result, setResult] = useState<FetchResult<WebhookOutboxRow[]> | null>(null)

  useEffect(() => {
    const token = tokens?.access_token
    if (!token) return
    let cancelled = false
    void (async () => {
      const res = await fetchWebhookOutbox(token)
      if (!cancelled) setResult(res)
    })()
    return () => {
      cancelled = true
    }
  }, [tokens?.access_token])

  return (
    <main className="sa-page">
      <h1>Webhook outbox</h1>
      <p className="lede">
        Recent outbox rows from <code>/api/v1/webhooks/outbox</code> (host only — no
        full destination URLs).
      </p>

      {result === null ? <p className="hint">Loading…</p> : null}
      {result && !result.available ? (
        <p className="hint">Webhook outbox unavailable ({result.detail}).</p>
      ) : null}
      {result?.available && result.data.length === 0 ? (
        <p className="hint">Outbox is empty.</p>
      ) : null}
      {result?.available && result.data.length > 0 ? (
        <div className="sa-table-wrap">
          <table className="sa-table">
            <thead>
              <tr>
                <th>Event</th>
                <th>Host</th>
                <th>Status</th>
                <th>Attempts</th>
              </tr>
            </thead>
            <tbody>
              {result.data.map((row) => (
                <tr key={row.id}>
                  <td>
                    <code>{row.event_type}</code>
                  </td>
                  <td>{row.destination_host}</td>
                  <td>
                    <span
                      className={`sa-pill sa-pill-${
                        row.status === 'delivered' ? 'live' : 'placeholder'
                      }`}
                    >
                      {row.status}
                    </span>
                  </td>
                  <td>
                    {row.attempts}/{row.max_attempts}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </main>
  )
}
