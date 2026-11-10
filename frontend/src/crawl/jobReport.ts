/**
 * Client-side crawl job report + multi-format export from tree/page snapshots.
 * Values come from Crawl.Api — AI never recomputes Domain Link Ratio here.
 */
import type { CrawlJob, PageNode } from '../api/crawl'
import type { CrawlPageSnapshot } from './ai/crawlAiApi'

export type CrawlReportRow = {
  pageId: number
  url: string
  depth: number
  status: string
  domainLinkRatio: number | null
  linkCount: number | null
  internalLinkCount: number | null
  error: string | null
}

export type CrawlJobReport = {
  jobId: string
  seedUrl: string
  status: string
  outcome: 'success' | 'partial' | 'empty' | 'failed'
  pagesCrawled: number
  pagesDone: number
  pagesFailed: number
  avgDomainLinkRatio: number | null
  minDomainLinkRatio: number | null
  maxDomainLinkRatio: number | null
  lowDlrPages: CrawlReportRow[]
  failedPages: CrawlReportRow[]
  rows: CrawlReportRow[]
  hints: string[]
}

export function flattenPageNodes(node: PageNode | null, acc: CrawlReportRow[] = []): CrawlReportRow[] {
  if (!node) return acc
  acc.push({
    pageId: node.pageId,
    url: node.url,
    depth: node.depth,
    status: node.status,
    domainLinkRatio: node.domainLinkRatio,
    linkCount: node.linkCount,
    internalLinkCount: node.internalLinkCount,
    error: node.error,
  })
  for (const child of node.children ?? []) {
    flattenPageNodes(child, acc)
  }
  return acc
}

export function snapshotsFromRows(rows: CrawlReportRow[]): CrawlPageSnapshot[] {
  return rows.map((r) => ({
    pageId: String(r.pageId),
    url: r.url,
    depth: r.depth,
    domainLinkRatio: r.domainLinkRatio,
    status: r.status,
    error: r.error,
  }))
}

export function buildJobReport(job: CrawlJob, root: PageNode | null): CrawlJobReport {
  const rows = flattenPageNodes(root)
  const withDlr = rows.filter((r) => r.domainLinkRatio != null) as (CrawlReportRow & {
    domainLinkRatio: number
  })[]
  const avgDlr =
    withDlr.length > 0
      ? withDlr.reduce((s, r) => s + r.domainLinkRatio, 0) / withDlr.length
      : null
  const dlrValues = withDlr.map((r) => r.domainLinkRatio)
  const pagesDone = job.pageCounts.done
  const pagesFailed = job.pageCounts.failed
  const hints: string[] = []

  let outcome: CrawlJobReport['outcome'] = 'success'
  if (job.status === 'Failed') outcome = 'failed'
  else if (pagesDone === 0) outcome = 'empty'
  else if (pagesFailed > 0 || job.pageCounts.skipped > 0) outcome = 'partial'

  if (pagesDone === 0) {
    hints.push('No HTML pages finished parsing — check the start URL, depth, or login wall.')
  } else if (pagesDone === 1 && job.maxDepth > 0) {
    hints.push('Only the seed page crawled — try higher max depth or confirm internal links exist.')
  }
  if (job.pageCounts.failed > 0) {
    hints.push(`${job.pageCounts.failed} page(s) failed — inspect errors in the tree and retry with login assist.`)
  }
  if (withDlr.length > 0 && avgDlr !== null && avgDlr < 0.15) {
    hints.push('Low average Domain Link Ratio — mostly external links; confirm the seed host matches target content.')
  }

  return {
    jobId: job.jobId,
    seedUrl: job.url,
    status: job.status,
    outcome,
    pagesCrawled: rows.length,
    pagesDone,
    pagesFailed,
    avgDomainLinkRatio: avgDlr,
    minDomainLinkRatio: dlrValues.length ? Math.min(...dlrValues) : null,
    maxDomainLinkRatio: dlrValues.length ? Math.max(...dlrValues) : null,
    lowDlrPages: withDlr.filter((r) => r.domainLinkRatio < 0.2),
    failedPages: rows.filter((r) => r.status === 'Failed' || r.error),
    rows,
    hints,
  }
}

export function diagnoseEmptyOrWeak(job: CrawlJob, report: CrawlJobReport): string[] {
  const tips: string[] = [...report.hints]
  try {
    const parsed = new URL(job.url)
    if (!job.url.startsWith('http')) {
      tips.push('Start URL should include https:// — the wizard can fix common typos.')
    }
    if (parsed.pathname === '/' && job.maxDepth >= 1 && report.pagesDone <= 1) {
      tips.push('Homepage may not link to the section you need — paste a deeper folder URL (e.g. /pages/).')
    }
  } catch {
    tips.push('URL could not be parsed — use a full https:// address.')
  }
  if (job.failureReason) {
    tips.push(`Job failure: ${job.failureReason}`)
  }
  if (report.pagesDone === 0 && job.pageCounts.processing === 0 && job.pageCounts.queued === 0) {
    tips.push('Worker queue drained with zero successes — likely blocked HTML, redirect loop, or non-HTML response.')
  }
  return [...new Set(tips)]
}

function downloadBlob(filename: string, mime: string, body: string) {
  const blob = new Blob([body], { type: mime })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = filename
  anchor.click()
  URL.revokeObjectURL(url)
}

export function exportJobReport(report: CrawlJobReport, format: 'csv' | 'json' | 'ndjson') {
  const stamp = report.jobId.slice(0, 8)
  if (format === 'json') {
    downloadBlob(
      `crawl-${stamp}.json`,
      'application/json',
      JSON.stringify(report, null, 2),
    )
    return
  }
  if (format === 'ndjson') {
    const lines = report.rows.map((r) => JSON.stringify(r)).join('\n')
    downloadBlob(`crawl-${stamp}.ndjson`, 'application/x-ndjson', lines)
    return
  }
  const header = [
    'pageId',
    'url',
    'depth',
    'status',
    'domainLinkRatio',
    'linkCount',
    'internalLinkCount',
    'error',
  ]
  const escape = (v: string | number | null) => {
    const s = v === null || v === undefined ? '' : String(v)
    if (s.includes(',') || s.includes('"') || s.includes('\n')) {
      return `"${s.replace(/"/g, '""')}"`
    }
    return s
  }
  const csv = [
    header.join(','),
    ...report.rows.map((r) =>
      [
        r.pageId,
        r.url,
        r.depth,
        r.status,
        r.domainLinkRatio,
        r.linkCount,
        r.internalLinkCount,
        r.error,
      ]
        .map(escape)
        .join(','),
    ),
  ].join('\n')
  downloadBlob(`crawl-${stamp}.csv`, 'text/csv', csv)
}
