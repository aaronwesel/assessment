/**
 * Superadmin overview — parallel skims of health, flags, audit, billing.
 */
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  fetchAuditSkim,
  fetchBillingSkim,
  fetchFlags,
  fetchHealthLive,
  fetchHealthReady,
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
      const [live, ready, flags, audit, billing] = await Promise.all([
        fetchHealthLive(),
        fetchHealthReady(),
        fetchFlags(token),
        fetchAuditSkim(token),
        fetchBillingSkim(token),
      ])

      if (cancelled) return

      const healthDetail = (() => {
        if (live.available && ready.available) {
          return `liveness ${live.data.status}; readiness ${ready.data.status}`
        }
        if (live.available) return `liveness ${live.data.status}`
        return live.detail
      })()

      setTiles([
        {
          title: 'Health / SLO',
          href: '/superadmin/health',
          status: live.available ? 'live' : 'placeholder',
          detail: healthDetail,
        },
        summarize('Feature flags', '/superadmin/flags', flags, (data) => {
          const items = normalizeFlags(data as Parameters<typeof normalizeFlags>[0])
          return items.length
            ? `${items.length} flag(s)`
            : 'Flags endpoint responded (empty)'
        }),
        summarize('Audit skim', '/superadmin/audit', audit, (data) => {
          const body = data as { total?: number; recent?: unknown[]; items?: unknown[] }
          const count =
            body.total ??
            body.recent?.length ??
            body.items?.length ??
            0
          return `${count} recent event(s)`
        }),
        summarize('Billing skim', '/superadmin/billing', billing, (data) => {
          const body = data as { status?: string; subscription_status?: string; plan?: string }
          return (
            body.subscription_status ||
            body.status ||
            body.plan ||
            'Billing summary available'
          )
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
        Platform dashboards for admins. Missing workstream APIs show as
        placeholders until A/B land.
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
