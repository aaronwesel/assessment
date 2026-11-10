import { useEffect, useState } from 'react'
import {
  fetchHealthLive,
  fetchHealthMetrics,
  fetchHealthReady,
  fetchHealthSlo,
  type FetchResult,
  type HealthLive,
  type HealthReady,
  type HealthSlo,
} from '../../api/platform'

export function SuperadminHealthPage() {
  const [live, setLive] = useState<FetchResult<HealthLive> | null>(null)
  const [ready, setReady] = useState<FetchResult<HealthReady> | null>(null)
  const [slo, setSlo] = useState<FetchResult<HealthSlo> | null>(null)
  const [metrics, setMetrics] = useState<FetchResult<Record<string, unknown>> | null>(
    null,
  )

  useEffect(() => {
    let cancelled = false
    void (async () => {
      const [l, r, s, m] = await Promise.all([
        fetchHealthLive(),
        fetchHealthReady(),
        fetchHealthSlo(),
        fetchHealthMetrics(),
      ])
      if (!cancelled) {
        setLive(l)
        setReady(r)
        setSlo(s)
        setMetrics(m)
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
        Liveness, readiness, and in-process SLO metrics from Platform A (
        <code>/health</code>, <code>/ready</code>, <code>/health/slo</code>).
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
        <h2>SLO <code>/health/slo</code></h2>
        {slo === null ? (
          <p className="hint">Checking…</p>
        ) : slo.available ? (
          <pre className="sa-pre">{JSON.stringify(slo.data, null, 2)}</pre>
        ) : (
          <p className="hint">{slo.detail}</p>
        )}
      </section>

      <section className="sa-section">
        <h2>Metrics <code>/health/metrics</code></h2>
        {metrics === null ? (
          <p className="hint">Checking…</p>
        ) : metrics.available ? (
          <pre className="sa-pre">{JSON.stringify(metrics.data, null, 2)}</pre>
        ) : (
          <p className="hint">{metrics.detail}</p>
        )}
      </section>
    </main>
  )
}
