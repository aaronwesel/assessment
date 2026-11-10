/**
 * Thin clients for platform ops APIs (workstream A/B).
 *
 * Paths match Platform A mounts under `/api/v1/*` plus root `/health*`.
 * Callers treat `available: false` as a placeholder skim (never throw for 404).
 * Do not log response bodies that could contain PII.
 */
import { config } from '../config'

function apiUrl(path: string): string {
  const base = config.apiBaseUrl.replace(/\/$/, '')
  return `${base}${path}`
}

export type FetchResult<T> =
  | { available: true; data: T }
  | { available: false; status: number; detail: string }

async function readDetail(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as { detail?: string }
    if (typeof body.detail === 'string') return body.detail
  } catch {
    /* ignore */
  }
  return `Request failed (${response.status})`
}

async function getJson<T>(
  path: string,
  accessToken?: string | null,
): Promise<FetchResult<T>> {
  try {
    const headers: Record<string, string> = {}
    if (accessToken) {
      headers.Authorization = `Bearer ${accessToken}`
    }
    const response = await fetch(apiUrl(path), { headers })
    if (response.status === 404 || response.status === 501) {
      return {
        available: false,
        status: response.status,
        detail: 'Endpoint not deployed yet',
      }
    }
    if (!response.ok) {
      return {
        available: false,
        status: response.status,
        detail: await readDetail(response),
      }
    }
    return { available: true, data: (await response.json()) as T }
  } catch {
    return { available: false, status: 0, detail: 'Network unavailable' }
  }
}

async function sendJson<T>(
  path: string,
  accessToken: string,
  method: 'PUT' | 'POST' | 'DELETE',
  body?: unknown,
): Promise<FetchResult<T>> {
  try {
    const response = await fetch(apiUrl(path), {
      method,
      headers: {
        Authorization: `Bearer ${accessToken}`,
        'Content-Type': 'application/json',
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
    if (!response.ok) {
      return {
        available: false,
        status: response.status,
        detail: await readDetail(response),
      }
    }
    if (response.status === 204) {
      return { available: true, data: undefined as T }
    }
    return { available: true, data: (await response.json()) as T }
  } catch {
    return { available: false, status: 0, detail: 'Network unavailable' }
  }
}

export type HealthLive = { status: string }

export type HealthReady = {
  status: string
  environment?: string
  redis_ok?: boolean
  db_ok?: boolean
  redis_connected?: boolean
  deepseek_enabled?: boolean
  rekognition_enabled?: boolean
  ses_enabled?: boolean
  slack_enabled?: boolean
  webhooks_enabled?: boolean
  feature_flags?: Record<string, boolean>
  [key: string]: unknown
}

export type HealthSlo = {
  status: string
  slo?: Record<string, unknown>
  availability?: number | string
  latency_ms_p95?: number
  requests_total?: number
  errors_5xx?: number
  [key: string]: unknown
}

export type FlagItem = {
  key: string
  enabled: boolean
  description?: string
  override?: boolean | null
  default?: boolean
}

/** Platform A returns a bare FlagOut[] from GET /api/v1/flags. */
export type FlagsPayload = FlagItem[] | { flags?: FlagItem[]; items?: FlagItem[] }

export type AuditEvent = {
  id?: string
  actor_user_id?: string | null
  action?: string
  resource_type?: string
  resource_id?: string | null
  request_id?: string | null
  metadata?: Record<string, unknown> | null
  created_at?: string | null
  [key: string]: unknown
}

export type AuditSkim = {
  items?: AuditEvent[]
  recent?: AuditEvent[]
  limit?: number
  offset?: number
  total?: number
  [key: string]: unknown
}

export type BillingSkim = {
  status?: string
  configured?: boolean
  customer_count?: number
  active_subscription_count?: number
  plan?: string | null
  customer_id?: string | null
  subscription_status?: string
  [key: string]: unknown
}

export type BreakGlassGrant = {
  id: string
  granted_by_user_id: string
  target_user_id: string
  reason_code: string
  ticket_ref?: string | null
  expires_at: string
  revoked_at?: string | null
  created_at?: string | null
  active: boolean
}

export type WebhookOutboxRow = {
  id: string
  event_type: string
  destination_host: string
  status: string
  attempts: number
  max_attempts: number
  last_status_code?: number | null
  last_error_code?: string | null
  next_attempt_at?: string | null
  delivered_at?: string | null
  created_at?: string | null
}

export async function fetchHealthLive(): Promise<FetchResult<HealthLive>> {
  return getJson<HealthLive>('/health')
}

export async function fetchHealthReady(): Promise<FetchResult<HealthReady>> {
  const primary = await getJson<HealthReady>('/health/ready')
  if (primary.available) return primary
  return getJson<HealthReady>('/ready')
}

export async function fetchHealthSlo(): Promise<FetchResult<HealthSlo>> {
  const primary = await getJson<HealthSlo>('/health/slo')
  if (primary.available) return primary
  return getJson<HealthSlo>('/api/v1/health/slo')
}

export async function fetchHealthMetrics(): Promise<FetchResult<Record<string, unknown>>> {
  const primary = await getJson<Record<string, unknown>>('/health/metrics')
  if (primary.available) return primary
  return getJson<Record<string, unknown>>('/api/v1/health/metrics')
}

export async function fetchFlags(
  accessToken: string,
): Promise<FetchResult<FlagsPayload>> {
  return getJson<FlagsPayload>('/api/v1/flags', accessToken)
}

export async function setFlag(
  accessToken: string,
  name: string,
  enabled: boolean,
): Promise<FetchResult<FlagItem>> {
  const result = await sendJson<{
    name: string
    enabled: boolean
    description?: string
    override?: boolean | null
    default?: boolean
  }>(`/api/v1/flags/${encodeURIComponent(name)}`, accessToken, 'PUT', {
    enabled,
  })
  if (!result.available) return result
  return {
    available: true,
    data: {
      key: result.data.name,
      enabled: result.data.enabled,
      description: result.data.description,
      override: result.data.override,
      default: result.data.default,
    },
  }
}

export async function clearFlag(
  accessToken: string,
  name: string,
): Promise<FetchResult<FlagItem>> {
  const result = await sendJson<{
    name: string
    enabled: boolean
    description?: string
    override?: boolean | null
    default?: boolean
  }>(`/api/v1/flags/${encodeURIComponent(name)}`, accessToken, 'DELETE')
  if (!result.available) return result
  return {
    available: true,
    data: {
      key: result.data.name,
      enabled: result.data.enabled,
      description: result.data.description,
      override: result.data.override,
      default: result.data.default,
    },
  }
}

export async function fetchAuditSkim(
  accessToken: string,
  limit = 20,
): Promise<FetchResult<AuditSkim>> {
  return getJson<AuditSkim>(
    `/api/v1/audit/events?limit=${limit}&offset=0`,
    accessToken,
  )
}

/** Trigger browser download of audit export (CSV or JSON). */
export async function downloadAuditExport(
  accessToken: string,
  format: 'csv' | 'json',
): Promise<{ ok: true } | { ok: false; detail: string }> {
  try {
    const response = await fetch(
      apiUrl(`/api/v1/audit/export?format=${format}&limit=1000`),
      { headers: { Authorization: `Bearer ${accessToken}` } },
    )
    if (!response.ok) {
      return { ok: false, detail: await readDetail(response) }
    }
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = `audit-export.${format}`
    anchor.click()
    URL.revokeObjectURL(url)
    return { ok: true }
  } catch {
    return { ok: false, detail: 'Network unavailable' }
  }
}

export async function fetchBillingSkim(
  accessToken: string,
): Promise<FetchResult<BillingSkim>> {
  const primary = await getJson<BillingSkim>(
    '/api/v1/billing/summary',
    accessToken,
  )
  if (primary.available) return primary
  return getJson<BillingSkim>('/api/v1/billing/me', accessToken)
}

export async function fetchBreakGlassGrants(
  accessToken: string,
  activeOnly = true,
): Promise<FetchResult<BreakGlassGrant[]>> {
  const q = activeOnly ? '?active_only=true&limit=20' : '?limit=20'
  return getJson<BreakGlassGrant[]>(`/api/v1/break-glass/grants${q}`, accessToken)
}

export async function fetchWebhookOutbox(
  accessToken: string,
): Promise<FetchResult<WebhookOutboxRow[]>> {
  return getJson<WebhookOutboxRow[]>('/api/v1/webhooks/outbox?limit=20', accessToken)
}

export function normalizeFlags(payload: FlagsPayload): FlagItem[] {
  if (Array.isArray(payload)) {
    return payload.map((item) => {
      const raw = item as FlagItem & { name?: string }
      return {
        key: raw.key || raw.name || 'unknown',
        enabled: Boolean(raw.enabled),
        description: raw.description,
        override: raw.override,
        default: raw.default,
      }
    })
  }
  const list = payload.flags ?? payload.items
  if (!Array.isArray(list)) return []
  return list.map((item) => {
    const raw = item as FlagItem & { name?: string }
    return {
      key: raw.key || raw.name || 'unknown',
      enabled: Boolean(raw.enabled),
      description: raw.description,
      override: raw.override,
      default: raw.default,
    }
  })
}
