/**
 * Crawl history — paginated jobs; open Job Details.
 */
import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import {
  crawlApi,
  CrawlApiError,
  type JobSummary,
  type PagedResult,
} from '../../api/crawl'
import { StatusBadge } from '../../components/crawl/StatusBadge'
import { formatDateTime } from '../../crawl/format'

const PAGE_SIZE = 20

export function HistoryPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const page = Math.max(1, Math.floor(Number(searchParams.get('page'))) || 1)
  const [data, setData] = useState<PagedResult<JobSummary> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(async () => {
    await Promise.resolve()
    setLoading(true)
    setError(null)
    try {
      setData(await crawlApi.getJobs(page, PAGE_SIZE))
    } catch (err) {
      setData(null)
      setError(
        err instanceof CrawlApiError ? err.message : 'Failed to load history.',
      )
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => {
    void load()
  }, [load])

  const goToPage = (next: number) => setSearchParams({ page: String(next) })

  return (
    <main className="page crawl-page crawl-page-wide">
      <div className="crawl-header">
        <h1>Crawl history</h1>
        <Link className="nav-link" to="/crawl">
          New crawl
        </Link>
      </div>

      {loading && !data ? <p className="hint">Loading history…</p> : null}

      {error ? (
        <div className="crawl-panel" role="alert">
          <p className="form-error">{error}</p>
          <button type="button" className="btn btn-ghost" onClick={() => void load()}>
            Retry
          </button>
        </div>
      ) : null}

      {!error && data && data.totalCount === 0 ? (
        <p className="hint">
          No crawls yet. <Link to="/crawl">Start one</Link>.
        </p>
      ) : null}

      {!error && data && data.totalCount > 0 ? (
        <>
          {data.items.length > 0 ? (
            <JobTable items={data.items} busy={loading} />
          ) : (
            <p className="hint">
              No jobs on this page.{' '}
              <Link to="/crawl/history">Go to the first page</Link>.
            </p>
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
            <span className="hint">
              Page {page} of {Math.max(1, Math.ceil(data.totalCount / data.pageSize))}{' '}
              ({data.totalCount} jobs)
            </span>
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => goToPage(page + 1)}
              disabled={
                page >= Math.max(1, Math.ceil(data.totalCount / data.pageSize)) ||
                loading
              }
            >
              Next
            </button>
          </nav>
        </>
      ) : null}
    </main>
  )
}

function JobTable({ items, busy }: { items: JobSummary[]; busy: boolean }) {
  const navigate = useNavigate()

  return (
    <div className="crawl-table-wrap">
      <table className="crawl-table" aria-busy={busy}>
        <thead>
          <tr>
            <th scope="col">URL</th>
            <th scope="col">Status</th>
            <th scope="col">Created</th>
            <th scope="col">Completed</th>
          </tr>
        </thead>
        <tbody>
          {items.map((job) => (
            <tr
              key={job.jobId}
              className="crawl-table-row"
              onClick={() => navigate(`/crawl/jobs/${job.jobId}`)}
            >
              <td>
                <Link to={`/crawl/jobs/${job.jobId}`}>{job.url}</Link>
              </td>
              <td>
                <StatusBadge status={job.status} />
              </td>
              <td>{formatDateTime(job.createdAt)}</td>
              <td>{formatDateTime(job.completedAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
