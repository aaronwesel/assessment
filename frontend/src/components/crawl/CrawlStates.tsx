import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'

export function CrawlLoading({ label = 'Loading…' }: { label?: string }) {
  return (
    <div className="crawl-state crawl-state-loading" role="status" aria-live="polite">
      <span className="crawl-spinner" aria-hidden="true" />
      <p>{label}</p>
    </div>
  )
}

export function CrawlEmpty({
  title,
  children,
  actionTo,
  actionLabel,
}: {
  title: string
  children?: ReactNode
  actionTo?: string
  actionLabel?: string
}) {
  return (
    <div className="crawl-state crawl-state-empty">
      <h2 className="crawl-state-title">{title}</h2>
      {children ? <p className="crawl-state-copy">{children}</p> : null}
      {actionTo && actionLabel ? (
        <Link className="btn btn-primary" to={actionTo}>
          {actionLabel}
        </Link>
      ) : null}
    </div>
  )
}

export function CrawlError({
  message,
  onRetry,
}: {
  message: string
  onRetry?: () => void
}) {
  return (
    <div className="crawl-state crawl-state-error" role="alert">
      <h2 className="crawl-state-title">Something went wrong</h2>
      <p className="crawl-state-copy">{message}</p>
      {onRetry ? (
        <button type="button" className="btn btn-primary" onClick={onRetry}>
          Try again
        </button>
      ) : null}
    </div>
  )
}
