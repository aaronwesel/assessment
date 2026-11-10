/**
 * SVG wordmark + trail-node icon (also used as favicon source in /public/favicon.svg).
 */
type Props = {
  compact?: boolean
  className?: string
}

export function TrailmapLogo({ compact, className = '' }: Props) {
  return (
    <span className={`trailmap-logo ${className}`.trim()} aria-hidden={compact}>
      <svg className="trailmap-logo-mark" viewBox="0 0 32 32" width={compact ? 28 : 36} height={compact ? 28 : 36}>
        <rect width="32" height="32" rx="8" fill="currentColor" className="trailmap-logo-bg" />
        <circle cx="16" cy="9" r="3.5" fill="#00e5b8" />
        <circle cx="9" cy="22" r="2.5" fill="#3d8bff" />
        <circle cx="23" cy="22" r="2.5" fill="#3d8bff" />
        <path stroke="#00e5b8" strokeWidth="1.5" strokeLinecap="round" d="M16 12.5v4M13.5 18l-2.5 2M18.5 18l2.5 2" />
      </svg>
      {!compact ? (
        <span className="trailmap-logo-type">
          <span className="trailmap-logo-name">Trailmap</span>
          <span className="trailmap-logo-sub">Crawl</span>
        </span>
      ) : null}
    </span>
  )
}
