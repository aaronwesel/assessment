/**
 * P0 Job Details value: crawl report, export, retry, empty diagnosis, billing hint.
 */
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import type { CrawlJob, PageNode } from '../../api/crawl'
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
  /** Step 6 guided crawl — surface export formats above the fold. */
  emphasizeExports?: boolean
}

export function JobResultsHub({ job, root, terminal, emphasizeExports }: Props) {
  const navigate = useNavigate()
  const { isAuthenticated } = useAuth()
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

  const estimatedCost =
    pricing && job.pageCounts.done > 0
      ? `${pricing.display_per_page} × ${job.pageCounts.done} pages`
      : null

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
          emphasizeExports={emphasizeExports}
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
  emphasizeExports,
}: {
  job: CrawlJob
  report: CrawlJobReport
  navigate: ReturnType<typeof useNavigate>
  isAuthenticated: boolean
  emphasizeExports?: boolean
}) {
  const hasExport = report.rows.length > 0

  const exportButtons = (
    <>
      <button
        type="button"
        className="btn btn-primary"
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
    </>
  )

  return (
    <div className="crawl-report-actions">
      {emphasizeExports ? (
        <div className="crawl-step6-export-card" aria-labelledby="step6-export-heading">
          <h3 id="step6-export-heading" className="crawl-step6-export-title">
            Export crawl data
          </h3>
          <p className="hint crawl-step6-export-lede">
            Download the page tree with Domain Link Ratio — client-side from values the .NET worker
            stored.
          </p>
          <div className="crawl-step6-export-buttons">{exportButtons}</div>
          {!hasExport ? (
            <p className="hint">No parsed pages to export yet.</p>
          ) : null}
        </div>
      ) : (
        <div className="crawl-export-row">
          <span className="hint">Export</span>
          {exportButtons}
        </div>
      )}

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
          <Link to="/register">Create a free account</Link> to attach crawls to your team and
          enable Stripe test billing per page. Anonymous crawls still work locally.
        </p>
      ) : (
        <p className="hint">
          <Link to="/billing">Billing settings</Link> — Stripe test mode for subscription; per-page
          meter display uses platform growth config.
        </p>
      )}
    </div>
  )
}

/** Tiny wrapper to apply crawl-rise without pulling motion lib */
function MotionWrap({ children }: { children: ReactNode }) {
  return <div className="crawl-rise-delay">{children}</div>
}
