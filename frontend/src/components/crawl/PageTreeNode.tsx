import { useId, useState } from 'react'
import type { PageNode } from '../../api/crawl'
import { formatPercent } from '../../crawl/format'
import { PageLinks } from './PageLinks'
import { StatusBadge } from './StatusBadge'

function DlrMeter({ ratio }: { ratio: number | null }) {
  if (ratio === null) {
    return <span className="crawl-dlr crawl-dlr-empty">DLR —</span>
  }
  const pct = Math.max(0, Math.min(100, ratio * 100))
  return (
    <span
      className="crawl-dlr"
      title="Domain Link Ratio: internal http(s) links on start host / total http(s) outgoing"
    >
      <span className="crawl-dlr-label">DLR {formatPercent(ratio)}</span>
      <span className="crawl-dlr-track" aria-hidden="true">
        <span className="crawl-dlr-fill" style={{ width: `${pct}%` }} />
      </span>
    </span>
  )
}

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
  const pathLabel = (() => {
    try {
      return new URL(node.url).pathname || '/'
    } catch {
      return node.url
    }
  })()

  return (
    <li className="crawl-tree-node" style={{ ['--depth' as string]: node.depth }}>
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

        <div className="crawl-tree-main">
          <a
            href={node.url}
            target="_blank"
            rel="noreferrer"
            className="crawl-tree-url"
            title={node.url}
          >
            <span className="crawl-tree-path">{pathLabel}</span>
            <span className="crawl-tree-host">{node.url}</span>
          </a>
          <div className="crawl-tree-meta">
            <StatusBadge status={node.status} />
            <span className="crawl-depth-chip">depth {node.depth}</span>
            <DlrMeter ratio={node.domainLinkRatio} />
            {node.linkCount !== null ? (
              <button
                type="button"
                className="btn btn-ghost crawl-link-toggle"
                aria-expanded={showLinks}
                aria-controls={linksId}
                onClick={() => setShowLinks((value) => !value)}
              >
                {node.linkCount} links · {node.internalLinkCount ?? 0} internal
              </button>
            ) : null}
          </div>
        </div>
      </div>

      {node.error ? <p className="form-error crawl-node-error">Error: {node.error}</p> : null}

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
