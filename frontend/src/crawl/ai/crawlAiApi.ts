/**
 * Client for Crawl.Ai overlay endpoints (hosted by Crawl.Api via MapCrawlAi).
 * Feature-flag aware; callers should hide panels when flags are off.
 */
import { config } from '../../config'

export type AiFeatureFlags = {
  enabled: boolean
  fieldFinderEnabled: boolean
  loginAssistEnabled: boolean
  summarizationEnabled: boolean
  askTheCrawlEnabled: boolean
  agentLoopEnabled: boolean
  deepSeekConfigured: boolean
}

export type FieldSuggestion = {
  label: string
  cssSelector?: string | null
  xpath?: string | null
  reason: string
  confidence: number
  sampleValue?: string | null
}

export type CrawlPageSnapshot = {
  pageId: string
  url: string
  depth: number
  domainLinkRatio?: number | null
  status?: string | null
  error?: string | null
}

async function parseJson<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const text = await res.text()
    throw new Error(text || `HTTP ${res.status}`)
  }
  return res.json() as Promise<T>
}

/** Crawl AI endpoints live on Crawl.Api (same host as jobs). */
function crawlAiBase(): string {
  return config.crawlApiBaseUrl.replace(/\/$/, '')
}

export async function fetchAiFeatures(): Promise<AiFeatureFlags> {
  const res = await fetch(`${crawlAiBase()}/api/ai/features`)
  const raw = await parseJson<Record<string, boolean>>(res)
  return {
    enabled: !!raw.enabled,
    fieldFinderEnabled: !!raw.fieldFinderEnabled,
    loginAssistEnabled: !!raw.loginAssistEnabled,
    summarizationEnabled: !!raw.summarizationEnabled,
    askTheCrawlEnabled: !!raw.askTheCrawlEnabled,
    agentLoopEnabled: !!raw.agentLoopEnabled,
    deepSeekConfigured: !!raw.deepSeekConfigured,
  }
}

export async function suggestFields(
  jobId: string,
  body: { pageId?: string; pageUrl?: string; htmlOrText: string; goalHint?: string },
) {
  const res = await fetch(`${crawlAiBase()}/api/ai/jobs/${jobId}/field-finder`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  return parseJson<{
    suggestions: FieldSuggestion[]
    usedModel: boolean
    degradedReason?: string | null
    requiresOperatorConfirmation: boolean
  }>(res)
}

export async function detectLogin(jobId: string, body: { pageUrl?: string; htmlOrText: string }) {
  const res = await fetch(`${crawlAiBase()}/api/ai/jobs/${jobId}/login-assist/detect`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  return parseJson<{
    detection: {
      detected: boolean
      formAction?: string | null
      guidedSteps: string[]
      fields: { name: string; type: string; isPassword: boolean }[]
    }
    auditEventId: string
  }>(res)
}

export async function confirmLoginStep(
  jobId: string,
  body: {
    auditEventId: string
    stepIndex: number
    approved: boolean
    credentialVaultRef?: string
    operatorId?: string
  },
) {
  const res = await fetch(`${crawlAiBase()}/api/ai/jobs/${jobId}/login-assist/confirm-step`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  return parseJson<{ status: string; message: string }>(res)
}

export async function summarizeJob(
  jobId: string,
  body: {
    seedUrl: string
    status: string
    pageCount: number
    edgeCount: number
    pages: CrawlPageSnapshot[]
    failures?: string[]
  },
) {
  const res = await fetch(`${crawlAiBase()}/api/ai/jobs/${jobId}/summarize`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  return parseJson<{
    summary: string
    highlights: string[]
    citedUrls: string[]
    usedModel: boolean
    degradedReason?: string | null
  }>(res)
}

export async function askTheCrawl(
  jobId: string,
  body: { question: string; pages: CrawlPageSnapshot[] },
) {
  const res = await fetch(`${crawlAiBase()}/api/ai/jobs/${jobId}/ask`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  return parseJson<{
    answer: string
    citations: { url: string; pageId?: string; snippet: string }[]
    usedModel: boolean
    degradedReason?: string | null
  }>(res)
}

export async function planAgent(jobId: string, body: { goal: string }) {
  const res = await fetch(`${crawlAiBase()}/api/ai/jobs/${jobId}/agent/plan`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  return parseJson<{
    planId: string // GUID string
    roleSignal: string
    planSteps: string[]
    proposedToolCalls: { toolName: string; purpose: string }[]
    requiresHumanConfirm: boolean
  }>(res)
}

export async function confirmAgent(
  jobId: string,
  body: { planId: string; approved: boolean; operatorId?: string; approvedToolNames?: string[] }, // planId GUID
) {
  const res = await fetch(`${crawlAiBase()}/api/ai/jobs/${jobId}/agent/confirm`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  return parseJson<{ status: string; executedTools: string[]; results: string[]; roleSignal: string }>(
    res,
  )
}
