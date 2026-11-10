import { useCallback, useEffect, useState } from 'react'
import {
  clearFlag,
  fetchFlags,
  normalizeFlags,
  setFlag,
  type FetchResult,
  type FlagItem,
  type FlagsPayload,
} from '../../api/platform'
import { useAuth } from '../../auth/AuthContext'

export function SuperadminFlagsPage() {
  const { tokens } = useAuth()
  const [result, setResult] = useState<FetchResult<FlagsPayload> | null>(null)
  const [flags, setFlags] = useState<FlagItem[]>([])
  const [busyKey, setBusyKey] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  const reload = useCallback(async () => {
    const token = tokens?.access_token
    if (!token) return
    const res = await fetchFlags(token)
    setResult(res)
    setFlags(res.available ? normalizeFlags(res.data) : [])
  }, [tokens?.access_token])

  useEffect(() => {
    let cancelled = false
    void (async () => {
      const token = tokens?.access_token
      if (!token) return
      const res = await fetchFlags(token)
      if (cancelled) return
      setResult(res)
      setFlags(res.available ? normalizeFlags(res.data) : [])
    })()
    return () => {
      cancelled = true
    }
  }, [tokens?.access_token])

  async function toggle(flag: FlagItem) {
    const token = tokens?.access_token
    if (!token) return
    setBusyKey(flag.key)
    setActionError(null)
    const res = await setFlag(token, flag.key, !flag.enabled)
    if (!res.available) {
      setActionError(res.detail)
    } else {
      await reload()
    }
    setBusyKey(null)
  }

  async function reset(flag: FlagItem) {
    const token = tokens?.access_token
    if (!token) return
    setBusyKey(flag.key)
    setActionError(null)
    const res = await clearFlag(token, flag.key)
    if (!res.available) {
      setActionError(res.detail)
    } else {
      await reload()
    }
    setBusyKey(null)
  }

  return (
    <main className="sa-page">
      <h1>Feature flags</h1>
      <p className="lede">
        Redis kill switches via <code>GET/PUT/DELETE /api/v1/flags</code>. Toggle
        forces an override; reset clears it back to settings default.
      </p>

      {result === null ? <p className="hint">Loading…</p> : null}
      {result && !result.available ? (
        <p className="hint">
          Flags API unavailable ({result.detail}). Expect{' '}
          <code>/api/v1/flags</code> from Platform A.
        </p>
      ) : null}
      {actionError ? <p className="form-error">{actionError}</p> : null}
      {result?.available && flags.length === 0 ? (
        <p className="hint">Flags endpoint responded with an empty list.</p>
      ) : null}

      {flags.length > 0 ? (
        <ul className="sa-flag-list">
          {flags.map((flag) => (
            <li key={flag.key} className="sa-flag-row sa-flag-row-actions">
              <div>
                <code>{flag.key}</code>
                {flag.description ? (
                  <span className="sa-tile-detail"> {flag.description}</span>
                ) : null}
                {flag.override != null ? (
                  <span className="hint"> · override={String(flag.override)}</span>
                ) : (
                  <span className="hint"> · default</span>
                )}
              </div>
              <div className="sa-flag-actions">
                <span className={`sa-pill sa-pill-${flag.enabled ? 'live' : 'placeholder'}`}>
                  {flag.enabled ? 'on' : 'off'}
                </span>
                <button
                  type="button"
                  className="btn btn-ghost"
                  disabled={busyKey === flag.key}
                  onClick={() => {
                    void toggle(flag)
                  }}
                >
                  {flag.enabled ? 'Force off' : 'Force on'}
                </button>
                <button
                  type="button"
                  className="btn btn-ghost"
                  disabled={busyKey === flag.key || flag.override == null}
                  onClick={() => {
                    void reset(flag)
                  }}
                >
                  Reset
                </button>
              </div>
            </li>
          ))}
        </ul>
      ) : null}
    </main>
  )
}
