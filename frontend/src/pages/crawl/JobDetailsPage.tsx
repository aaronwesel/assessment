/**
 * Job Details — status, timestamps, progress polling; tree + DLR when complete.
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

  const loadJob = useCallback(async () => {
    if (!jobId) return
    await Promise.resolve()
    setLoading(true)
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
      setLoading(false)
    }
  }, [jobId])

  useEffect(() => {
    void loadJob()
  }, [loadJob])

  useEffect(() => {
    if (!job || !isActiveJobStatus(job.status)) return
    const timer = window.setInterval(() => {
      void loadJob()
    }, POLL_MS)
    return () => window.clearInterval(timer)
  }, [job, loadJob])

  return (
    <main className="page crawl-page crawl-page-wide">
      <div className="crawl-header">
        <h1>Job details</h1>
        <div className="crawl-header-links">
          <Link className="nav-link" to="/crawl">
            New crawl
          </Link>
          <Link className="nav-link" to="/crawl/history">
            History
          </Link>
        </div>
      </div>

      {loading && !job ? <p className="hint">Loading job…</p> : null}

      {error && !job ? (
        <div className="crawl-panel" role="alert">
          <p className="form-error">{error}</p>
          <button type="button" className="btn btn-ghost" onClick={() => void loadJob()}>
            Retry
          </button>
        </div>
      ) : null}

      {job ? (
        <>
          <JobSummary job={job} onCanceled={() => void loadJob()} />
          {job.status === 'Completed' ? (
            <JobTreeSection jobId={job.jobId} job={job} />
          ) : null}
          {isActiveJobStatus(job.status) ? (
            <p className="hint crawl-poll-hint">Polling every {POLL_MS / 1000}s…</p>
          ) : null}
        </>
      ) : null}
    </main>
  )
}

function JobSummary({
  job,
  onCanceled,
}: {
  job: CrawlJob
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
    <section className="crawl-panel" aria-labelledby="job-summary-heading">
      <h2 id="job-summary-heading" className="crawl-section-title">
        Summary
      </h2>
      <dl className="home-meta crawl-meta">
        <div>
          <dt>Status</dt>
          <dd>
            <StatusBadge status={job.status} />
          </dd>
        </div>
        <div>
          <dt>URL</dt>
          <dd>
            <a href={job.url} target="_blank" rel="noreferrer">
              {job.url}
            </a>
          </dd>
        </div>
        <div>
          <dt>Limits</dt>
          <dd>
            depth {job.maxDepth}, up to {job.maxPages} pages
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
        {job.failureReason ? (
          <div>
            <dt>Failure reason</dt>
            <dd className="form-error">{job.failureReason}</dd>
          </div>
        ) : null}
      </dl>
      <JobProgress counts={job.pageCounts} />
      {isActiveJobStatus(job.status) ? (
        <button
          type="button"
          className="btn btn-ghost"
          onClick={() => void cancel()}
          disabled={cancelPending}
        >
          {cancelPending ? 'Canceling…' : 'Cancel job'}
        </button>
      ) : null}
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
      <section className="crawl-panel" aria-labelledby="tree-heading">
        <h2 id="tree-heading" className="crawl-section-title">
          Page tree
        </h2>
        {loading ? <p className="hint">Loading page tree…</p> : null}
        {error ? (
          <div role="alert">
            <p className="form-error">{error}</p>
            <button type="button" className="btn btn-ghost" onClick={() => void load()}>
              Retry
            </button>
          </div>
        ) : null}
        {!loading && !error && tree?.root ? (
          <PageTree jobId={jobId} root={tree.root} />
        ) : null}
        {!loading && !error && tree && !tree.root ? (
          <p className="hint">No pages were crawled for this job.</p>
        ) : null}
      </section>
      {!loading && !error ? (
        <section className="crawl-panel" aria-label="Assistive AI">
          <AiAssistPanels
            jobId={jobId}
            seedUrl={job.url}
            status={job.status}
            pages={pages}
            pageCount={job.pageCounts.total}
            edgeCount={pages.reduce((n, p) => n + (p.domainLinkRatio != null ? 1 : 0), 0)}
          />
        </section>
      ) : null}
    </>
  )
}
