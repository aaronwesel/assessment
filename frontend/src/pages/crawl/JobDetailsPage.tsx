/**
 * Job Details — live progress, readable tree + DLR, assistive AI when available.
 */
import { useCallback, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  crawlApi,
  CrawlApiError,
  isActiveJobStatus,
  type CrawlJob,
  type JobTree,
  type PageNode,
} from '../../api/crawl'
import { CrawlShell } from '../../components/crawl/CrawlShell'
import {
  CrawlEmpty,
  CrawlError,
  CrawlLoading,
} from '../../components/crawl/CrawlStates'
import { JobProgress } from '../../components/crawl/JobProgress'
import { PageTree } from '../../components/crawl/PageTree'
import { StatusBadge } from '../../components/crawl/StatusBadge'
import { AiAssistPanels } from '../../crawl/ai/AiAssistPanels'
import type { CrawlPageSnapshot } from '../../crawl/ai/crawlAiApi'
import { formatDateTime } from '../../crawl/format'

const POLL_MS = 1500

export function JobDetailsPage() {
  const { jobId = '' } = useParams()
  const [job, setJob] = useState<CrawlJob | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const liveStatus = job?.status

  const loadJob = useCallback(
    async (opts?: { silent?: boolean }) => {
      if (!jobId) return null
      if (!opts?.silent) {
        await Promise.resolve()
        setLoading(true)
      }
      try {
        const next = await crawlApi.getJob(jobId)
        setJob(next)
        setError(null)
        return next
      } catch (err) {
        setError(
          err instanceof CrawlApiError ? err.message : 'Failed to load job.',
        )
        return null
      } finally {
        if (!opts?.silent) setLoading(false)
      }
    },
    [jobId],
  )

  useEffect(() => {
    void loadJob()
  }, [loadJob])

  useEffect(() => {
    if (!liveStatus || !isActiveJobStatus(liveStatus)) return
    const timer = window.setInterval(() => {
      void loadJob({ silent: true })
    }, POLL_MS)
    return () => window.clearInterval(timer)
  }, [liveStatus, loadJob])

  const live = !!job && isActiveJobStatus(job.status)

  return (
    <CrawlShell
      title="Job details"
      lede={
        live
          ? 'Live crawl in progress — progress refreshes automatically.'
          : 'Status, timestamps, page tree, and Domain Link Ratio for this job.'
      }
      wide
      actions={
        <div className="crawl-console-actions-row">
          <Link className="btn btn-ghost" to="/crawl/history">
            History
          </Link>
          <Link className="btn btn-primary" to="/crawl">
            New crawl
          </Link>
        </div>
      }
    >
      {loading && !job ? <CrawlLoading label="Loading job…" /> : null}

      {error && !job ? (
        <CrawlError message={error} onRetry={() => void loadJob()} />
      ) : null}

      {job ? (
        <div className="crawl-details-layout crawl-rise-delay">
          <JobSummary job={job} live={live} onCanceled={() => void loadJob()} />
          {job.status === 'Completed' || job.status === 'Failed' || job.status === 'Canceled' ? (
            <JobTreeSection jobId={job.jobId} job={job} />
          ) : (
            <section className="crawl-panel-block" aria-labelledby="tree-waiting">
              <h2 id="tree-waiting" className="crawl-section-title">
                Page tree
              </h2>
              <CrawlEmpty title="Tree unlocks when the job finishes">
                Keep this tab open — progress updates every {POLL_MS / 1000}s. Domain
                Link Ratio appears on each page once parsing completes.
              </CrawlEmpty>
            </section>
          )}
        </div>
      ) : null}
    </CrawlShell>
  )
}

function JobSummary({
  job,
  live,
  onCanceled,
}: {
  job: CrawlJob
  live: boolean
  onCanceled: () => void
}) {
  const [cancelPending, setCancelPending] = useState(false)
  const [cancelError, setCancelError] = useState<string | null>(null)

  const cancel = async () => {
    setCancelPending(true)
    setCancelError(null)
    try {
      await crawlApi.cancelJob(job.jobId)
      onCanceled()
    } catch (err) {
      setCancelError(
        err instanceof CrawlApiError ? err.message : 'Cancel failed.',
      )
    } finally {
      setCancelPending(false)
    }
  }

  return (
    <section className="crawl-panel-block" aria-labelledby="job-summary-heading">
      <div className="crawl-summary-head">
        <h2 id="job-summary-heading" className="crawl-section-title">
          Summary
        </h2>
        <StatusBadge status={job.status} />
      </div>

      <p className="crawl-summary-url">
        <a href={job.url} target="_blank" rel="noreferrer">
          {job.url}
        </a>
      </p>

      <dl className="crawl-fact-grid">
        <div>
          <dt>Limits</dt>
          <dd>
            depth {job.maxDepth} · up to {job.maxPages} pages
          </dd>
        </div>
        <div>
          <dt>Created</dt>
          <dd>{formatDateTime(job.createdAt)}</dd>
        </div>
        <div>
          <dt>Started</dt>
          <dd>{formatDateTime(job.startedAt)}</dd>
        </div>
        <div>
          <dt>Completed</dt>
          <dd>{formatDateTime(job.completedAt)}</dd>
        </div>
      </dl>

      {job.failureReason ? (
        <p className="form-error" role="alert">
          {job.failureReason}
        </p>
      ) : null}

      <JobProgress
        counts={job.pageCounts}
        progressPercent={job.progressPercent}
        live={live}
      />

      <div className="crawl-summary-actions">
        {live ? (
          <button
            type="button"
            className="btn btn-ghost"
            onClick={() => void cancel()}
            disabled={cancelPending}
          >
            {cancelPending ? 'Canceling…' : 'Cancel job'}
          </button>
        ) : (
          <Link className="btn btn-primary" to="/crawl">
            Run another crawl
          </Link>
        )}
        {live ? (
          <span className="hint crawl-poll-hint">
            <span className="crawl-live-dot" aria-hidden="true" />
            Updating every {POLL_MS / 1000}s
          </span>
        ) : null}
      </div>

      {cancelError ? (
        <p className="form-error" role="alert">
          {cancelError}
        </p>
      ) : null}
    </section>
  )
}

function flattenPages(node: PageNode | null, acc: CrawlPageSnapshot[] = []): CrawlPageSnapshot[] {
  if (!node) return acc
  acc.push({
    pageId: String(node.pageId),
    url: node.url,
    depth: node.depth,
    domainLinkRatio: node.domainLinkRatio,
    status: node.status,
    error: node.error,
  })
  for (const child of node.children ?? []) {
    flattenPages(child, acc)
  }
  return acc
}

function JobTreeSection({ jobId, job }: { jobId: string; job: CrawlJob }) {
  const [tree, setTree] = useState<JobTree | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(async () => {
    await Promise.resolve()
    setLoading(true)
    setError(null)
    try {
      setTree(await crawlApi.getJobTree(jobId))
    } catch (err) {
      setTree(null)
      setError(
        err instanceof CrawlApiError ? err.message : 'Failed to load page tree.',
      )
    } finally {
      setLoading(false)
    }
  }, [jobId])

  useEffect(() => {
    void load()
  }, [load])

  const pages = flattenPages(tree?.root ?? null)

  return (
    <>
      <section className="crawl-panel-block" aria-labelledby="tree-heading">
        <div className="crawl-summary-head">
          <h2 id="tree-heading" className="crawl-section-title">
            Page tree
          </h2>
          <span className="hint">Domain Link Ratio per page</span>
        </div>

        {loading ? <CrawlLoading label="Loading page tree…" /> : null}

        {error ? (
          <CrawlError message={error} onRetry={() => void load()} />
        ) : null}

        {!loading && !error && tree?.root ? (
          <PageTree jobId={jobId} root={tree.root} />
        ) : null}

        {!loading && !error && tree && !tree.root ? (
          <CrawlEmpty title="No pages crawled" actionTo="/crawl" actionLabel="Start another crawl">
            The job finished without a crawlable root page.
          </CrawlEmpty>
        ) : null}
      </section>

      {!loading && !error ? (
        <section className="crawl-panel-block crawl-ai-wrap" aria-label="Assistive AI">
          <AiAssistPanels
            jobId={jobId}
            seedUrl={job.url}
            status={job.status}
            pages={pages}
            pageCount={job.pageCounts.total}
            edgeCount={pages.reduce(
              (n, p) => n + (p.domainLinkRatio != null ? 1 : 0),
              0,
            )}
          />
        </section>
      ) : null}
    </>
  )
}
