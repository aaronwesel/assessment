import { useCallback, useEffect, useState } from 'react'
import { crawlApi, CrawlApiError, type PageLink } from '../../api/crawl'

export function PageLinks({ jobId, pageId }: { jobId: string; pageId: number }) {
  const [links, setLinks] = useState<PageLink[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(async () => {
    await Promise.resolve()
    setLoading(true)
    setError(null)
    try {
      setLinks(await crawlApi.getPageLinks(jobId, pageId))
    } catch (err) {
      setLinks(null)
      setError(
        err instanceof CrawlApiError
          ? err.message
          : 'Failed to load outgoing links.',
      )
    } finally {
      setLoading(false)
    }
  }, [jobId, pageId])

  useEffect(() => {
    void load()
  }, [load])

  if (loading) return <p className="hint">Loading links…</p>
  if (error) {
    return (
      <div className="crawl-inline-error" role="alert">
        <p className="form-error">{error}</p>
        <button type="button" className="btn btn-ghost" onClick={() => void load()}>
          Retry
        </button>
      </div>
    )
  }
  if (!links || links.length === 0) {
    return <p className="hint">No outgoing links.</p>
  }

  return (
    <ul className="crawl-links">
      {links.map((link) => (
        <li key={link.url}>
          <span
            className={
              link.isInternal ? 'crawl-tag crawl-tag-internal' : 'crawl-tag crawl-tag-external'
            }
          >
            {link.isInternal ? 'internal' : 'external'}
          </span>{' '}
          <a href={link.url} target="_blank" rel="noreferrer">
            {link.url}
          </a>
        </li>
      ))}
    </ul>
  )
}
