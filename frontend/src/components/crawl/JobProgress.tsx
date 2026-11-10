import type { PageCounts } from '../../api/crawl'

export function JobProgress({
  counts,
  progressPercent,
  live = false,
}: {
  counts: PageCounts
  progressPercent?: number
  live?: boolean
}) {
  const finished = counts.done + counts.skipped + counts.failed
  const total = Math.max(counts.total, 1)
  const pct =
    progressPercent !== undefined
      ? Math.min(100, Math.max(0, Math.round(progressPercent)))
      : Math.min(100, Math.round((finished / total) * 100))
  const donePct = (counts.done / total) * 100
  const skippedPct = (counts.skipped / total) * 100
  const failedPct = (counts.failed / total) * 100

  return (
    <div className={`crawl-progress${live ? ' is-live' : ''}`}>
      <div className="crawl-progress-head">
        <span className="crawl-progress-title">
          {live ? <span className="crawl-live-dot" aria-hidden="true" /> : null}
          {finished} of {counts.total} pages
        </span>
        <span className="crawl-progress-pct" aria-hidden="true">
          {pct}%
        </span>
      </div>
      <div
        className="crawl-progress-track"
        role="progressbar"
        aria-valuemin={0}
        aria-valuemax={counts.total}
        aria-valuenow={finished}
        aria-label={`Crawl progress: ${finished} of ${counts.total} pages`}
      >
        <span className="crawl-progress-seg is-done" style={{ width: `${donePct}%` }} />
        <span
          className="crawl-progress-seg is-skipped"
          style={{ width: `${skippedPct}%` }}
        />
        <span
          className="crawl-progress-seg is-failed"
          style={{ width: `${failedPct}%` }}
        />
      </div>
      <ul className="crawl-progress-legend">
        <li>
          <span className="crawl-swatch is-done" />
          {counts.done} done
        </li>
        <li>
          <span className="crawl-swatch is-queued" />
          {counts.queued} queued
        </li>
        <li>
          <span className="crawl-swatch is-processing" />
          {counts.processing} processing
        </li>
        <li>
          <span className="crawl-swatch is-skipped" />
          {counts.skipped} skipped
        </li>
        <li>
          <span className="crawl-swatch is-failed" />
          {counts.failed} failed
        </li>
      </ul>
    </div>
  )
}
