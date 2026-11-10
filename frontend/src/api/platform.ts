/**
 * Thin clients for platform ops APIs (workstream A/B).
 *
 * Endpoints may be missing until sibling packages land — callers treat
 * `available: false` as a placeholder skim, never throw for 404.
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
        detail: `Request failed (${response.status})`,
      }
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
  [key: string]: unknown
}

export type FlagItem = {
  key: string
  enabled: boolean
  description?: string
}

export type FlagsPayload = {
  flags?: FlagItem[]
  items?: FlagItem[]
  [key: string]: unknown
}

export type AuditSkim = {
  total?: number
  recent?: Array<Record<string, unknown>>
  items?: Array<Record<string, unknown>>
  [key: string]: unknown
}

export type BillingSkim = {
  status?: string
  plan?: string
  customer_id?: string
  subscription_status?: string
  [key: string]: unknown
}

export async function fetchHealthLive(): Promise<FetchResult<HealthLive>> {
  return getJson<HealthLive>('/health')
}

export async function fetchHealthReady(): Promise<FetchResult<HealthReady>> {
  return getJson<HealthReady>('/health/ready')
}

/** Prefer /api/v1/flags; fall back to /api/v1/feature-flags. */
export async function fetchFlags(
  accessToken: string,
): Promise<FetchResult<FlagsPayload>> {
  const primary = await getJson<FlagsPayload>('/api/v1/flags', accessToken)
  if (primary.available) return primary
  return getJson<FlagsPayload>('/api/v1/feature-flags', accessToken)
}

/** Prefer /api/v1/audit; fall back to /api/v1/audits/recent. */
export async function fetchAuditSkim(
  accessToken: string,
): Promise<FetchResult<AuditSkim>> {
  const primary = await getJson<AuditSkim>('/api/v1/audit?limit=10', accessToken)
  if (primary.available) return primary
  return getJson<AuditSkim>('/api/v1/audits/recent', accessToken)
}

/** Prefer /api/v1/billing/summary; fall back to /api/v1/billing/me. */
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

export function normalizeFlags(payload: FlagsPayload): FlagItem[] {
  const list = payload.flags ?? payload.items
  if (Array.isArray(list)) return list
  return []
}
