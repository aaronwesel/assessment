import { useEffect, useState } from 'react'
import {
  fetchFlags,
  normalizeFlags,
  type FetchResult,
  type FlagItem,
  type FlagsPayload,
} from '../../api/platform'
import { useAuth } from '../../auth/AuthContext'

export function SuperadminFlagsPage() {
  const { tokens } = useAuth()
  const [result, setResult] = useState<FetchResult<FlagsPayload> | null>(null)
  const [flags, setFlags] = useState<FlagItem[]>([])

  useEffect(() => {
    const token = tokens?.access_token
    if (!token) return
    let cancelled = false
    void (async () => {
      const res = await fetchFlags(token)
      if (cancelled) return
      setResult(res)
      setFlags(res.available ? normalizeFlags(res.data) : [])
    })()
    return () => {
      cancelled = true
    }
  }, [tokens?.access_token])

  return (
    <main className="sa-page">
      <h1>Feature flags</h1>
      <p className="lede">
        Kill switches from Redis-backed flag APIs when workstream A deploys them.
      </p>

      {result === null ? <p className="hint">Loading…</p> : null}
      {result && !result.available ? (
        <p className="hint">
          Flags API unavailable ({result.detail}). Placeholder until{' '}
          <code>/api/v1/flags</code> lands.
        </p>
      ) : null}
      {result?.available && flags.length === 0 ? (
        <p className="hint">Flags endpoint responded with an empty list.</p>
      ) : null}

      {flags.length > 0 ? (
        <ul className="sa-flag-list">
          {flags.map((flag) => (
            <li key={flag.key} className="sa-flag-row">
              <code>{flag.key}</code>
              <span className={`sa-pill sa-pill-${flag.enabled ? 'live' : 'placeholder'}`}>
                {flag.enabled ? 'on' : 'off'}
              </span>
              {flag.description ? (
                <span className="sa-tile-detail">{flag.description}</span>
              ) : null}
            </li>
          ))}
        </ul>
      ) : null}
    </main>
  )
}
