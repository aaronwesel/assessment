import { useEffect, useState } from 'react'
import {
  fetchHealthLive,
  fetchHealthReady,
  type FetchResult,
  type HealthLive,
  type HealthReady,
} from '../../api/platform'

export function SuperadminHealthPage() {
  const [live, setLive] = useState<FetchResult<HealthLive> | null>(null)
  const [ready, setReady] = useState<FetchResult<HealthReady> | null>(null)

  useEffect(() => {
    let cancelled = false
    void (async () => {
      const [l, r] = await Promise.all([fetchHealthLive(), fetchHealthReady()])
      if (!cancelled) {
        setLive(l)
        setReady(r)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [])

  return (
    <main className="sa-page">
      <h1>Health / SLO</h1>
      <p className="lede">
        Liveness and readiness probes. SLO targets are placeholders until metrics
        land.
      </p>

      <section className="sa-section">
        <h2>Liveness <code>/health</code></h2>
        {live === null ? (
          <p className="hint">Checking…</p>
        ) : live.available ? (
          <pre className="sa-pre">{JSON.stringify(live.data, null, 2)}</pre>
        ) : (
          <p className="hint">{live.detail}</p>
        )}
      </section>

      <section className="sa-section">
        <h2>Readiness <code>/health/ready</code></h2>
        {ready === null ? (
          <p className="hint">Checking…</p>
        ) : ready.available ? (
          <pre className="sa-pre">{JSON.stringify(ready.data, null, 2)}</pre>
        ) : (
          <p className="hint">{ready.detail}</p>
        )}
      </section>

      <section className="sa-section">
        <h2>SLO targets (placeholder)</h2>
        <ul className="sa-list">
          <li>API availability ≥ 99.9% monthly</li>
          <li>p95 latency &lt; 300ms for auth/me</li>
          <li>Error budget: 43m / 30d</li>
        </ul>
      </section>
    </main>
  )
}
