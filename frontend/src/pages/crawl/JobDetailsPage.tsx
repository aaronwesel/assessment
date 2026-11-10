/**
 * Job Details — live progress (SignalR + REST fallback), report, tree + DLR, export, AI assist.
 */
import { useCallback, useEffect, useMemo, useState } from 'react'
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
import { ElementPickExport } from '../../components/crawl/ElementPickExport'
import { JobProgress } from '../../components/crawl/JobProgress'
import { JobResultsHub } from '../../components/crawl/JobResultsHub'
import { PageTree } from '../../components/crawl/PageTree'
import { StatusBadge } from '../../components/crawl/StatusBadge'
import { AiAssistPanels, IncidentsPanel } from '../../crawl/ai/AiAssistPanels'
import type { CrawlPageSnapshot } from '../../crawl/ai/crawlAiApi'
import { formatDateTime } from '../../crawl/format'
import { useJobRealtime, type LiveTransport } from '../../crawl/useJobRealtime'

export function JobDetailsPage() {
  const { jobId = '' } = useParams()
  const [job, setJob] = useState<CrawlJob | null>(null)
  const [tree, setTree] = useState<JobTree | null>(null)
  const [treeError, setTreeError] = useState<string | null>(null)
  const [treeLoading, setTreeLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

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

  const loadTree = useCallback(
    async (opts?: { silent?: boolean }) => {
      if (!jobId) return
      if (!opts?.silent) {
        setTreeLoading(true)
        setTreeError(null)
      }
      try {
        const next = await crawlApi.getJobTree(jobId)
        setTree(next)
        setTreeError(null)
      } catch (err) {
        setTree(null)
        setTreeError(
          err instanceof CrawlApiError ? err.message : 'Failed to load page tree.',
        )
      } finally {
        if (!opts?.silent) setTreeLoading(false)
      }
    },
    [jobId],
  )

  const silentRefreshJob = useCallback(() => loadJob({ silent: true }), [loadJob])
  const silentRefreshTree = useCallback(() => loadTree({ silent: true }), [loadTree])

  useEffect(() => {
    void loadJob()
    void loadTree()
  }, [loadJob, loadTree])

  const live = !!job && isActiveJobStatus(job.status)
  const { transport, pollMs } = useJobRealtime({
    jobId,
    job,
    setJob,
    isLive: live,
    refreshJob: silentRefreshJob,
    refreshTree: silentRefreshTree,
  })

  const terminal = !!job && !isActiveJobStatus(job.status)
  const root = tree?.root ?? null

  const pages = useMemo(() => flattenPages(root), [root])

  return (
    <CrawlShell
      title="Job details"
      lede={
        live
          ? 'Live crawl — report and tree update as pages finish.'
          : 'Crawl report, exports, Domain Link Ratio tree, and AI assist.'
      }
      wide
      actions={
        <div className="crawl-console-actions-row">
          <Link className="btn btn-ghost" to="/crawl/history">
            History
          </Link>
          <Link className="btn btn-ghost" to="/crawl/welcome">
            Wizard
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
          <JobSummary
            job={job}
            live={live}
            transport={transport}
            pollMs={pollMs}
            onCanceled={() => void loadJob()}
          />

          <JobResultsHub job={job} root={root} terminal={terminal} />

          <section className="crawl-panel-block crawl-ai-wrap" aria-label="Crawl incidents">
            <IncidentsPanel jobId={job.jobId} />
          </section>

          <JobTreeSection
            jobId={job.jobId}
            live={live}
            root={root}
            loading={treeLoading}
            error={treeError}
            onRetry={() => void loadTree()}
          />

          {root ? <ElementPickExport jobId={job.jobId} root={root} /> : null}

          {pages.length > 0 ? (
            <section className="crawl-panel-block crawl-ai-wrap" aria-label="Assistive AI">
              <AiAssistPanels
                jobId={job.jobId}
                seedUrl={job.url}
                status={job.status}
                pages={pages}
                pageCount={job.pageCounts.total}
                edgeCount={pages.length}
              />
            </section>
          ) : null}
        </div>
      ) : null}
    </CrawlShell>
  )
}

function JobSummary({
  job,
  live,
  transport,
  pollMs,
  onCanceled,
}: {
  job: CrawlJob
  live: boolean
  transport: LiveTransport
  pollMs: number
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
          <Link className="btn btn-primary" to="/crawl/welcome">
            Run guided retry
          </Link>
        )}
        {live ? (
          <span className="hint crawl-poll-hint" data-transport={transport}>
            <span className="crawl-live-dot" aria-hidden="true" />
            {transportLabel(transport, pollMs)}
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

function JobTreeSection({
  jobId,
  live,
  root,
  loading,
  error,
  onRetry,
}: {
  jobId: string
  live: boolean
  root: PageNode | null
  loading: boolean
  error: string | null
  onRetry: () => void
}) {
  return (
    <section className="crawl-panel-block" aria-labelledby="tree-heading">
      <div className="crawl-summary-head">
        <h2 id="tree-heading" className="crawl-section-title">
          Page tree
        </h2>
        <span className="hint">
          {live ? 'Grows as pages complete' : 'Domain Link Ratio per page'}
        </span>
      </div>

      {loading && !root ? <CrawlLoading label="Loading page tree…" /> : null}

      {error ? <CrawlError message={error} onRetry={onRetry} /> : null}

      {!loading && !error && root ? <PageTree jobId={jobId} root={root} /> : null}

      {!loading && !error && !root ? (
        <CrawlEmpty title="No pages in tree yet" actionTo="/crawl/welcome" actionLabel="Try wizard">
          {live
            ? 'The worker is still fetching the seed page — keep this tab open.'
            : 'The job finished without a crawlable root page. Use the crawl report above for diagnosis.'}
        </CrawlEmpty>
      ) : null}
    </section>
  )
}

function transportLabel(transport: LiveTransport, pollMs: number): string {
  switch (transport) {
    case 'signalr':
      return 'Live via SignalR'
    case 'connecting':
      return 'Connecting live channel…'
    case 'polling':
      return `REST polling every ${pollMs / 1000}s`
    default:
      return 'Live updates'
  }
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
