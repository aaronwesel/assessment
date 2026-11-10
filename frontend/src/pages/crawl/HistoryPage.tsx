/**
 * Crawl history — paginated jobs with clear empty/error paths and next actions.
 */
import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import {
  crawlApi,
  CrawlApiError,
  isActiveJobStatus,
  type JobSummary,
  type PagedResult,
} from '../../api/crawl'
import { CrawlShell } from '../../components/crawl/CrawlShell'
import {
  CrawlEmpty,
  CrawlError,
  CrawlLoading,
} from '../../components/crawl/CrawlStates'
import { StatusBadge } from '../../components/crawl/StatusBadge'
import { formatDateTime } from '../../crawl/format'

const PAGE_SIZE = 20
const HISTORY_ACTIVE_POLL_MS = 4000

export function HistoryPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const page = Math.max(1, Math.floor(Number(searchParams.get('page'))) || 1)
  const [data, setData] = useState<PagedResult<JobSummary> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(async (opts?: { silent?: boolean }) => {
    if (!opts?.silent) {
      setLoading(true)
      setError(null)
    }
    try {
      setData(await crawlApi.getJobs(page, PAGE_SIZE))
      if (!opts?.silent) setError(null)
    } catch (err) {
      if (!opts?.silent) {
        setData(null)
        setError(
          err instanceof CrawlApiError ? err.message : 'Failed to load history.',
        )
      }
    } finally {
      if (!opts?.silent) setLoading(false)
    }
  }, [page])

  useEffect(() => {
    void load()
  }, [load])

  const hasActiveJobs = Boolean(data?.items.some((j) => isActiveJobStatus(j.status)))

  useEffect(() => {
    if (!hasActiveJobs || error) return undefined
    const timer = window.setInterval(() => {
      void load({ silent: true })
    }, HISTORY_ACTIVE_POLL_MS)
    return () => window.clearInterval(timer)
  }, [hasActiveJobs, error, load])

  const goToPage = (next: number) => setSearchParams({ page: String(next) })
  const totalPages = data
    ? Math.max(1, Math.ceil(data.totalCount / data.pageSize))
    : 1

  return (
    <CrawlShell
      title="Crawl history"
      lede="Recent jobs, newest first. In-flight crawls keep running server-side — rows refresh until they complete."
      wide
      actions={
        <Link className="btn btn-primary" to="/crawl">
          Start crawl
        </Link>
      }
    >
      {loading && !data ? <CrawlLoading label="Loading crawl history…" /> : null}

      {error ? (
        <CrawlError message={error} onRetry={() => void load()} />
      ) : null}

      {!error && data && data.totalCount === 0 ? (
        <CrawlEmpty
          title="No crawls yet"
          actionTo="/crawl"
          actionLabel="Start your first crawl"
        >
          History fills as jobs complete. Start from a public HTML URL to see the
          tree and Domain Link Ratio.
        </CrawlEmpty>
      ) : null}

      {!error && data && data.totalCount > 0 ? (
        <section className="crawl-history-panel crawl-rise-delay" aria-label="Jobs">
          {data.items.length > 0 ? (
            <JobList items={data.items} busy={loading} />
          ) : (
            <CrawlEmpty
              title="Nothing on this page"
              actionTo="/crawl/history"
              actionLabel="Back to first page"
            >
              Try an earlier page of results.
            </CrawlEmpty>
          )}

          <nav className="crawl-pager" aria-label="Pagination">
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => goToPage(page - 1)}
              disabled={page <= 1 || loading}
            >
              Previous
            </button>
            <span className="crawl-pager-status">
              Page {page} of {totalPages}
              <span className="hint"> · {data.totalCount} jobs</span>
            </span>
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => goToPage(page + 1)}
              disabled={page >= totalPages || loading}
            >
              Next
            </button>
          </nav>
        </section>
      ) : null}
    </CrawlShell>
  )
}

function JobList({ items, busy }: { items: JobSummary[]; busy: boolean }) {
  const navigate = useNavigate()

  return (
    <ul className="crawl-job-list" aria-busy={busy}>
      {items.map((job) => (
        <li key={job.jobId}>
          <button
            type="button"
            className="crawl-job-row"
            onClick={() => navigate(`/crawl/jobs/${job.jobId}`)}
          >
            <div className="crawl-job-row-main">
              <span className="crawl-job-url">{job.url}</span>
              <span className="crawl-job-times">
                Created {formatDateTime(job.createdAt)}
                {job.completedAt
                  ? ` · Completed ${formatDateTime(job.completedAt)}`
                  : ''}
              </span>
            </div>
            <StatusBadge status={job.status} />
            <span className="crawl-job-open" aria-hidden="true">
              Open
            </span>
          </button>
          <Link className="visually-hidden" to={`/crawl/jobs/${job.jobId}`}>
            Open job {job.url}
          </Link>
        </li>
      ))}
    </ul>
  )
}
