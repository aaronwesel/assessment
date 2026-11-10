/**
 * Crawl.Api client — job create/status/tree/history/cancel.
 *
 * Base URL from `VITE_CRAWL_API_URL` (separate from FastAPI platform auth).
 * Contract mirrors ASP.NET camelCase JSON: POST/GET `/api/jobs`, tree + links.
 * Auth is optional locally; do not log response bodies (URLs may be sensitive).
 */
import { config } from '../config'

export type JobStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Canceled'
/** Matches Crawl.Domain.Pages.CrawlPageStatus (stringified). */
export type PageStatus = 'Queued' | 'Processing' | 'Done' | 'Skipped' | 'Failed'

export interface PageCounts {
  total: number
  queued: number
  processing: number
  done: number
  skipped: number
  failed: number
}

export interface CrawlJob {
  jobId: string
  url: string
  status: JobStatus
  maxDepth: number
  maxPages: number
  createdAt: string
  startedAt: string | null
  completedAt: string | null
  failureReason: string | null
  pageCounts: PageCounts
  progressPercent: number
}

export interface PageNode {
  pageId: number
  url: string
  depth: number
  status: PageStatus
  linkCount: number | null
  internalLinkCount: number | null
  domainLinkRatio: number | null
  error: string | null
  children: PageNode[]
}

export interface JobTree {
  jobId: string
  status: JobStatus
  root: PageNode | null
}

export interface PageLink {
  url: string
  isInternal: boolean
}

export interface PageRow {
  pageId: number
  url: string
  depth: number
  status: PageStatus
  domainLinkRatio: number | null
  linkCount: number | null
  internalLinkCount: number | null
  error: string | null
  parentPageId: number | null
}

export interface JobSummary {
  jobId: string
  url: string
  status: JobStatus
  createdAt: string
  completedAt: string | null
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export interface CreateJobRequest {
  url: string
  maxDepth?: number
}

export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  /** ASP.NET ValidationProblem field map (camelCase keys). */
  errors?: Record<string, string[]>
  /** Cancel conflict body: `{ error: "Job already Completed." }`. */
  error?: string
}

export class CrawlApiError extends Error {
  status: number
  problem: ProblemDetails

  constructor(status: number, problem: ProblemDetails) {
    const fieldMsgs = problem.errors
      ? Object.values(problem.errors).flat().filter(Boolean)
      : []
    super(
      problem.detail ??
        problem.error ??
        (fieldMsgs.length ? fieldMsgs.join(' ') : undefined) ??
        problem.title ??
        `Request failed (${status})`,
    )
    this.name = 'CrawlApiError'
    this.status = status
    this.problem = problem
  }
}

export function isActiveJobStatus(status: JobStatus | undefined): boolean {
  return status === 'Pending' || status === 'Running'
}

function crawlUrl(path: string): string {
  const base = config.crawlApiBaseUrl.replace(/\/$/, '')
  return `${base}${path}`
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response
  try {
    response = await fetch(crawlUrl(path), {
      ...init,
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
        ...init?.headers,
      },
    })
  } catch {
    throw new CrawlApiError(0, {
      title: 'Network error',
      detail: 'Could not reach Crawl.Api. Check VITE_CRAWL_API_URL.',
    })
  }

  if (!response.ok) {
    const problem: ProblemDetails = await response.json().catch(() => ({}))
    throw new CrawlApiError(response.status, {
      title: response.statusText,
      ...problem,
    })
  }

  if (response.status === 204) {
    return undefined as T
  }
  return (await response.json()) as T
}

export const crawlApi = {
  createJob: (body: CreateJobRequest) =>
    request<{ jobId: string }>('/api/jobs', {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  getJob: (jobId: string) =>
    request<CrawlJob>(`/api/jobs/${encodeURIComponent(jobId)}`),

  getJobTree: (jobId: string) =>
    request<JobTree>(`/api/jobs/${encodeURIComponent(jobId)}/tree`),

  getJobPages: (jobId: string) =>
    request<PageRow[]>(`/api/jobs/${encodeURIComponent(jobId)}/pages`),

  getPageLinks: (jobId: string, pageId: number) =>
    request<PageLink[]>(
      `/api/jobs/${encodeURIComponent(jobId)}/pages/${pageId}/links`,
    ),

  getJobs: (page: number, pageSize: number) =>
    request<PagedResult<JobSummary>>(
      `/api/jobs?page=${page}&pageSize=${pageSize}`,
    ),

  cancelJob: (jobId: string) =>
    request<void>(`/api/jobs/${encodeURIComponent(jobId)}/cancel`, {
      method: 'POST',
    }),
}
