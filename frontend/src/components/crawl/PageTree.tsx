import type { PageNode } from '../../api/crawl'
import { PageTreeNode } from './PageTreeNode'

export function PageTree({ jobId, root }: { jobId: string; root: PageNode }) {
  return (
    <ul className="crawl-tree" aria-label="Crawled pages">
      <PageTreeNode jobId={jobId} node={root} />
    </ul>
  )
}
