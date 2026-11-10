import { useId, useState } from 'react'
import type { PageNode } from '../../api/crawl'
import { formatPercent } from '../../crawl/format'
import { PageLinks } from './PageLinks'
import { StatusBadge } from './StatusBadge'

export function PageTreeNode({
  jobId,
  node,
}: {
  jobId: string
  node: PageNode
}) {
  const [expanded, setExpanded] = useState(node.depth === 0)
  const [showLinks, setShowLinks] = useState(false)
  const childrenId = useId()
  const linksId = useId()
  const hasChildren = node.children.length > 0

  return (
    <li className="crawl-tree-node">
      <div className="crawl-tree-row">
        {hasChildren ? (
          <button
            type="button"
            className="crawl-toggle"
            aria-expanded={expanded}
            aria-controls={childrenId}
            aria-label={`${expanded ? 'Collapse' : 'Expand'} ${node.url}`}
            onClick={() => setExpanded((value) => !value)}
          >
            {expanded ? '▾' : '▸'}
          </button>
        ) : (
          <span className="crawl-toggle" aria-hidden="true" />
        )}
        <a href={node.url} target="_blank" rel="noreferrer" className="crawl-tree-url">
          {node.url}
        </a>
        <StatusBadge status={node.status} />
        <span className="hint">depth {node.depth}</span>
        <span title="Domain Link Ratio: internal links / total links">
          DLR {formatPercent(node.domainLinkRatio)}
        </span>
        {node.linkCount !== null && (
          <button
            type="button"
            className="btn btn-ghost crawl-link-toggle"
            aria-expanded={showLinks}
            aria-controls={linksId}
            onClick={() => setShowLinks((value) => !value)}
          >
            {node.linkCount} links ({node.internalLinkCount ?? 0} internal)
          </button>
        )}
      </div>
      {node.error ? <p className="form-error">Error: {node.error}</p> : null}
      {showLinks ? (
        <div id={linksId} className="crawl-tree-links">
          <PageLinks jobId={jobId} pageId={node.pageId} />
        </div>
      ) : null}
      {hasChildren && expanded ? (
        <ul id={childrenId} className="crawl-tree">
          {node.children.map((child) => (
            <PageTreeNode key={child.pageId} jobId={jobId} node={child} />
          ))}
        </ul>
      ) : null}
    </li>
  )
}
