import type { JobStatus, PageStatus } from '../../api/crawl'

export function StatusBadge({ status }: { status: JobStatus | PageStatus }) {
  return (
    <span className={`crawl-badge crawl-badge-${status.toLowerCase()}`}>
      {status}
    </span>
  )
}
