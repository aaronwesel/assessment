import type { PageCounts } from '../../api/crawl'

export function JobProgress({ counts }: { counts: PageCounts }) {
  const finished = counts.done + counts.skipped + counts.failed
  const total = Math.max(counts.total, 1)

  return (
    <div className="crawl-progress">
      <label className="crawl-progress-label">
        Progress: {finished} of {counts.total} pages
        <progress value={finished} max={total} />
      </label>
      <p className="hint">
        {counts.done} done · {counts.skipped} skipped · {counts.failed} failed ·{' '}
        {counts.queued} queued
      </p>
    </div>
  )
}
