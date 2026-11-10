/**
 * Job Details — live progress, actionable report, tree + DLR, export, element pick, AI assist.
 */
import { useCallback, useMemo, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import {
  crawlApi,
  CrawlApiError,
  isActiveJobStatus,
  type CrawlJob,
  type JobTree,
  type PageNode,
} from '../../api/crawl'
import { CrawlFlowSteps } from '../../components/crawl/CrawlFlowSteps'
import { CrawlShell } from '../../components/crawl/CrawlShell'
import { DeepSeekGuide } from '../../components/crawl/DeepSeekGuide'
import {
  CrawlEmpty,
  CrawlError,
  CrawlLoading,
} from '../../components/crawl/CrawlStates'
import { ElementPickExport } from '../../components/crawl/ElementPickExport'
import { JobAlertPreference } from '../../components/crawl/JobAlertPreference'
import { JobProgress } from '../../components/crawl/JobProgress'
import { JobResultsHub } from '../../components/crawl/JobResultsHub'
import { PageTree } from '../../components/crawl/PageTree'
import { StatusBadge } from '../../components/crawl/StatusBadge'
import { AiAssistPanels, IncidentsPanel } from '../../crawl/ai/AiAssistPanels'
import type { CrawlPageSnapshot } from '../../crawl/ai/crawlAiApi'
import { formatDateTime } from '../../crawl/format'
import { JOB_DETAILS_POLL_MS, useJobLiveRefresh } from '../../crawl/useJobLiveRefresh'

export function JobDetailsPage() {
  const { jobId = '' } = useParams()
  const [searchParams] = useSearchParams()
  const guided = searchParams.get('guided') === '1'
  const [job, setJob] = useState<CrawlJob | null>(null)
  const [tree, setTree] = useState<JobTree | null>(null)
  const [treeError, setTreeError] = useState<string | null>(null)
  const [treeLoading, setTreeLoading] = useState(true)
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

  useJobLiveRefresh({ jobId, liveStatus, loadJob, loadTree })

  const live = !!job && isActiveJobStatus(job.status)
  const terminal = !!job && !isActiveJobStatus(job.status)
  const root = tree?.root ?? null

  const pages = useMemo(() => flattenPages(root), [root])

  return (
    <CrawlShell
      title="Job details"
      lede={
        live
          ? 'Step 5 — watch the worker queue pages. Results, tree, exports, and element pick unlock when the job finishes.'
          : 'Step 6 — crawl report, exports, Domain Link Ratio tree, element pick, and AI assist.'
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
          {guided || live || terminal ? (
            <CrawlFlowSteps activeStep={live ? 5 : 6} maxReachable={6} />
          ) : null}

          <div className="crawl-details-guided-row">
            <div className="crawl-details-primary">
          <JobSummary
            job={job}
            live={live}
            guided={guided}
            terminal={terminal}
            onCanceled={() => void loadJob()}
            onAlertPreferenceSaved={(next) => setJob(next)}
          />

          {live ? (
            <section
              className="crawl-panel-block crawl-step5-running"
              aria-labelledby="step5-running-heading"
            >
              <h2 id="step5-running-heading" className="crawl-section-title">
                Crawl running
              </h2>
              <p className="hint">
                Stay on this tab — step 6 opens automatically with the full report, page tree, DLR,
                file exports, and element pick once the job reaches a finished state.
              </p>
            </section>
          ) : null}

          <section className="crawl-panel-block crawl-ai-wrap" aria-label="Crawl incidents">
            <IncidentsPanel jobId={job.jobId} />
          </section>

          {terminal ? (
            <>
              <JobResultsHub job={job} root={root} terminal={terminal} emphasizeExports />

              <JobTreeSection
                jobId={job.jobId}
                live={false}
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
            </>
          ) : null}
            </div>
            <DeepSeekGuide
              step={live ? 4 : 5}
              url={job.url}
              maxDepth={job.maxDepth}
              collapsed={!guided}
            />
          </div>
        </div>
      ) : null}
    </CrawlShell>
  )
}

function JobSummary({
  job,
  live,
  guided,
  terminal,
  onCanceled,
  onAlertPreferenceSaved,
}: {
  job: CrawlJob
  live: boolean
  guided: boolean
  terminal: boolean
  onCanceled: () => void
  onAlertPreferenceSaved: (job: CrawlJob) => void
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

      {guided || live ? (
        <JobAlertPreference
          jobId={job.jobId}
          initialEmail={job.alertEmail}
          statusAlertSent={job.statusAlertSent}
          terminal={terminal}
          onSaved={onAlertPreferenceSaved}
        />
      ) : null}

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
          <span className="hint crawl-poll-hint">
            <span className="crawl-live-dot" aria-hidden="true" />
            Live updates every {JOB_DETAILS_POLL_MS / 1000}s
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
