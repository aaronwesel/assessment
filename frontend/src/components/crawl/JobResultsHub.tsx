/**
 * P0 Job Details value: crawl report, export, retry, empty diagnosis, billing hint.
 */
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import type { CrawlJob, PageNode } from '../../api/crawl'
import { recordCrawlUsage } from '../../api/billing'
import { fetchCrawlPricing, type CrawlPricing } from '../../api/growth'
import { summarizeJob } from '../../crawl/ai/crawlAiApi'
import {
  buildJobReport,
  diagnoseEmptyOrWeak,
  exportJobReport,
  snapshotsFromRows,
  type CrawlJobReport,
} from '../../crawl/jobReport'
import { formatPercent } from '../../crawl/format'

type Props = {
  job: CrawlJob
  root: PageNode | null
  terminal: boolean
}

export function JobResultsHub({ job, root, terminal }: Props) {
  const navigate = useNavigate()
  const { isAuthenticated, tokens } = useAuth()
  const [pricing, setPricing] = useState<CrawlPricing | null>(null)
  const [aiNote, setAiNote] = useState<string | null>(null)
  const [aiBusy, setAiBusy] = useState(false)

  const report = useMemo(() => buildJobReport(job, root), [job, root])
  const tips = useMemo(() => diagnoseEmptyOrWeak(job, report), [job, report])

  useEffect(() => {
    let cancelled = false
    void fetchCrawlPricing().then((p) => {
      if (!cancelled) setPricing(p)
    })
    return () => {
      cancelled = true
    }
  }, [])

  useEffect(() => {
    if (!terminal || report.outcome === 'success') return
    let cancelled = false
    ;(async () => {
      setAiBusy(true)
      setAiNote(null)
      try {
        const failures = report.failedPages.map((p) => p.error ?? p.url)
        const resp = await summarizeJob(job.jobId, {
          seedUrl: job.url,
          status: job.status,
          pageCount: job.pageCounts.total,
          edgeCount: report.rows.length,
          pages: snapshotsFromRows(report.rows),
          failures,
        })
        if (cancelled) return
        setAiNote(
          [resp.summary, ...resp.highlights.map((h) => `• ${h}`)].filter(Boolean).join('\n'),
        )
      } catch (e) {
        if (!cancelled) setAiNote((e as Error).message)
      } finally {
        if (!cancelled) setAiBusy(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [terminal, job, report])

  const freePages = pricing?.free_pages_allowance ?? 5000
  const pagesWithResults = job.pageCounts.done
  const estimatedCost =
    pricing && pagesWithResults > 0
      ? `${pagesWithResults} pages-with-results × ${pricing.display_per_page} (meter: ${freePages.toLocaleString()} free)`
      : null

  useEffect(() => {
    if (!terminal || !isAuthenticated || !tokens?.access_token || pagesWithResults <= 0) return
    let cancelled = false
    void recordCrawlUsage(tokens.access_token, {
      crawl_job_id: job.jobId,
      pages_with_results: pagesWithResults,
    }).catch(() => {
      if (!cancelled) {
        /* billing record is best-effort in test mode */
      }
    })
    return () => {
      cancelled = true
    }
  }, [terminal, isAuthenticated, tokens?.access_token, job.jobId, pagesWithResults])

  return (
    <section className="crawl-panel-block crawl-results-hub" aria-labelledby="crawl-results-heading">
      <MotionWrap>
        <div className="crawl-summary-head">
          <h2 id="crawl-results-heading" className="crawl-section-title">
            Crawl report
          </h2>
          <OutcomeBadge outcome={report.outcome} status={job.status} />
        </div>

        <dl className="crawl-report-grid">
          <div>
            <dt>Pages parsed</dt>
            <dd>
              {job.pageCounts.done} done · {job.pageCounts.failed} failed · {job.pageCounts.skipped}{' '}
              skipped
            </dd>
          </div>
          <div className="crawl-dlr-block">
            <dt>Domain Link Ratio (DLR)</dt>
            <dd>
              {report.avgDomainLinkRatio !== null ? (
                <>
                  On average,{' '}
                  <strong>{formatPercent(report.avgDomainLinkRatio)}</strong> of links on each crawled page
                  stayed on the same site (range {formatPercent(report.minDomainLinkRatio)} –{' '}
                  {formatPercent(report.maxDomainLinkRatio)}).
                </>
              ) : (
                '— DLR appears after the .NET worker parses links on a page.'
              )}
            </dd>
            <p className="hint crawl-dlr-hint">
              DLR = internal links ÷ all links on that page. AI summaries may quote these values but never
              recompute them.
            </p>
          </div>
          {estimatedCost ? (
            <div>
              <dt>Estimated usage (test)</dt>
              <dd>
                {estimatedCost}
                {!isAuthenticated ? (
                  <span className="hint"> — register to save job history & billing</span>
                ) : null}
              </dd>
            </div>
          ) : null}
        </dl>

        {tips.length > 0 ? (
          <div className="crawl-diagnosis" role="status">
            <h3 className="crawl-diagnosis-title">What went wrong?</h3>
            <ul className="crawl-diagnosis-list">
              {tips.map((t) => (
                <li key={t}>{t}</li>
              ))}
            </ul>
            {aiBusy ? <p className="hint">AI diagnosis…</p> : null}
            {aiNote ? (
              <div className="crawl-ai-diagnosis">
                <h4>AI summary</h4>
                <pre className="crawl-ai-diagnosis-text">{aiNote}</pre>
              </div>
            ) : null}
          </div>
        ) : null}

        <ReportActions
          job={job}
          report={report}
          navigate={navigate}
          isAuthenticated={isAuthenticated}
          freePages={freePages}
        />
      </MotionWrap>
    </section>
  )
}

function OutcomeBadge({
  outcome,
  status,
}: {
  outcome: CrawlJobReport['outcome']
  status: string
}) {
  const label =
    outcome === 'success'
      ? 'Healthy crawl'
      : outcome === 'partial'
        ? 'Partial results'
        : outcome === 'empty'
          ? 'No results'
          : 'Failed'
  return (
    <span className={`crawl-outcome crawl-outcome-${outcome}`} data-status={status}>
      {label}
    </span>
  )
}

function ReportActions({
  job,
  report,
  navigate,
  isAuthenticated,
  freePages,
}: {
  job: CrawlJob
  report: CrawlJobReport
  navigate: ReturnType<typeof useNavigate>
  isAuthenticated: boolean
  freePages: number
}) {
  const hasExport = report.rows.length > 0

  return (
    <div className="crawl-report-actions">
      <div className="crawl-export-row">
        <span className="hint">Export</span>
        <button
          type="button"
          className="btn btn-ghost"
          disabled={!hasExport}
          onClick={() => exportJobReport(report, 'csv')}
        >
          CSV
        </button>
        <button
          type="button"
          className="btn btn-ghost"
          disabled={!hasExport}
          onClick={() => exportJobReport(report, 'json')}
        >
          JSON
        </button>
        <button
          type="button"
          className="btn btn-ghost"
          disabled={!hasExport}
          onClick={() => exportJobReport(report, 'ndjson')}
        >
          NDJSON
        </button>
        <button
          type="button"
          className="btn btn-ghost"
          disabled={!hasExport}
          onClick={() => exportJobReport(report, 'md')}
        >
          Markdown
        </button>
        <button
          type="button"
          className="btn btn-ghost"
          disabled={!hasExport}
          onClick={() => exportJobReport(report, 'html')}
        >
          HTML
        </button>
      </div>

      <div className="crawl-retry-row">
        <span className="hint">Retry</span>
        <button
          type="button"
          className="btn btn-primary"
          onClick={() =>
            navigate(
              `/crawl/welcome?url=${encodeURIComponent(job.url)}&depth=${job.maxDepth + 1}&advanced=1&retry=depth`,
            )
          }
        >
          Try again with more options
        </button>
        <button
          type="button"
          className="btn btn-ghost"
          onClick={() =>
            navigate(
              `/crawl/welcome?url=${encodeURIComponent(job.url)}&depth=${job.maxDepth}&retry=login`,
            )
          }
        >
          Login wall assist
        </button>
        <Link className="btn btn-ghost" to="/crawl">
          Quick start
        </Link>
      </div>

      {!isAuthenticated ? (
        <p className="crawl-save-cta hint">
          <Link to="/register">Create a free account</Link> for {freePages.toLocaleString()} free pages-with-results,
          then Stripe test per-page billing. Anonymous crawls still work locally.
        </p>
      ) : (
        <p className="hint">
          <Link to="/billing">Billing settings</Link> — {freePages.toLocaleString()} free pages-with-results, then
          Stripe test meter at the published per-page rate.
        </p>
      )}
    </div>
  )
}

/** Tiny wrapper to apply crawl-rise without pulling motion lib */
function MotionWrap({ children }: { children: ReactNode }) {
  return <div className="crawl-rise-delay">{children}</div>
}
