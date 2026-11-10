/**
 * Superadmin overview — parallel skims of Platform A/B APIs.
 */
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  fetchAuditSkim,
  fetchBillingSkim,
  fetchBreakGlassGrants,
  fetchFlags,
  fetchHealthLive,
  fetchHealthReady,
  fetchHealthSlo,
  fetchWebhookOutbox,
  normalizeFlags,
  type FetchResult,
} from '../../api/platform'
import { useAuth } from '../../auth/AuthContext'

type Tile = {
  title: string
  href: string
  status: string
  detail: string
}

function summarize(
  title: string,
  href: string,
  result: FetchResult<unknown>,
  whenOk: (data: unknown) => string,
): Tile {
  if (!result.available) {
    return {
      title,
      href,
      status: 'placeholder',
      detail: result.detail,
    }
  }
  return {
    title,
    href,
    status: 'live',
    detail: whenOk(result.data),
  }
}

export function SuperadminOverviewPage() {
  const { tokens } = useAuth()
  const [tiles, setTiles] = useState<Tile[] | null>(null)

  useEffect(() => {
    const token = tokens?.access_token
    if (!token) return
    let cancelled = false

    void (async () => {
      const [live, ready, slo, flags, audit, billing, bg, hooks] = await Promise.all([
        fetchHealthLive(),
        fetchHealthReady(),
        fetchHealthSlo(),
        fetchFlags(token),
        fetchAuditSkim(token, 10),
        fetchBillingSkim(token),
        fetchBreakGlassGrants(token, true),
        fetchWebhookOutbox(token),
      ])

      if (cancelled) return

      const healthDetail = (() => {
        const parts: string[] = []
        if (live.available) parts.push(`live ${live.data.status}`)
        if (ready.available) parts.push(`ready ${ready.data.status}`)
        if (slo.available) {
          const avail = slo.data.availability
          const p95 = slo.data.latency_ms_p95
          if (avail != null) parts.push(`avail ${String(avail)}`)
          if (p95 != null) parts.push(`p95 ${p95}ms`)
        }
        if (parts.length) return parts.join(' · ')
        if (!live.available) return live.detail
        return 'health ok'
      })()

      setTiles([
        {
          title: 'Health / SLO',
          href: '/superadmin/health',
          status: live.available || ready.available || slo.available ? 'live' : 'placeholder',
          detail: healthDetail,
        },
        summarize('Feature flags', '/superadmin/flags', flags, (data) => {
          const items = normalizeFlags(data as Parameters<typeof normalizeFlags>[0])
          return items.length
            ? `${items.length} flag(s)`
            : 'Flags endpoint responded (empty)'
        }),
        summarize('Audit skim', '/superadmin/audit', audit, (data) => {
          const body = data as { items?: unknown[]; recent?: unknown[] }
          const count = body.items?.length ?? body.recent?.length ?? 0
          return `${count} recent event(s)`
        }),
        summarize('Billing skim', '/superadmin/billing', billing, (data) => {
          const body = data as {
            active_subscription_count?: number
            customer_count?: number
            subscription_status?: string
            status?: string
          }
          if (typeof body.active_subscription_count === 'number') {
            return `${body.active_subscription_count} active · ${body.customer_count ?? 0} customers`
          }
          return body.subscription_status || body.status || 'Billing summary available'
        }),
        summarize('Break-glass', '/superadmin/break-glass', bg, (data) => {
          const list = data as unknown[]
          return `${list.length} active grant(s)`
        }),
        summarize('Webhook outbox', '/superadmin/webhooks', hooks, (data) => {
          const list = data as unknown[]
          return `${list.length} outbox row(s)`
        }),
      ])
    })()

    return () => {
      cancelled = true
    }
  }, [tokens?.access_token])

  return (
    <main className="sa-page">
      <h1>Console</h1>
      <p className="lede">
        Platform dashboards for admins. Wired to Platform A/B APIs; unavailable
        endpoints show as placeholders.
      </p>
      <div className="sa-tiles">
        {(tiles ?? []).map((tile) => (
          <Link key={tile.href} to={tile.href} className="sa-tile">
            <span className={`sa-pill sa-pill-${tile.status}`}>{tile.status}</span>
            <strong>{tile.title}</strong>
            <span className="sa-tile-detail">{tile.detail}</span>
          </Link>
        ))}
        {tiles === null ? <p className="hint">Loading platform skims…</p> : null}
      </div>
    </main>
  )
}
