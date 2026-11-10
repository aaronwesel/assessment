/**
 * Shared crawl console chrome — brand signal, secondary nav, atmosphere.
 * Keeps Start / Details / History feeling like one product surface.
 */
import type { ReactNode } from 'react'
import { Link, NavLink } from 'react-router-dom'

type Props = {
  title: string
  lede?: string
  wide?: boolean
  children: ReactNode
  actions?: ReactNode
}

export function CrawlShell({ title, lede, wide, children, actions }: Props) {
  return (
    <main className={`crawl-console${wide ? ' crawl-console-wide' : ''}`}>
      <div className="crawl-atmosphere" aria-hidden="true">
        <div className="crawl-orb crawl-orb-a" />
        <div className="crawl-orb crawl-orb-b" />
        <div className="crawl-mesh" />
      </div>

      <div className="crawl-console-inner crawl-rise">
        <header className="crawl-console-top">
          <div className="crawl-brand-block">
            <p className="crawl-kicker">Assessment · Crawl</p>
            <h1 className="crawl-console-title">{title}</h1>
            {lede ? <p className="crawl-lede">{lede}</p> : null}
          </div>
          {actions ? <div className="crawl-console-actions">{actions}</div> : null}
        </header>

        <nav className="crawl-subnav" aria-label="Crawl console">
          <NavLink
            to="/crawl"
            end
            className={({ isActive }) =>
              `crawl-subnav-link${isActive ? ' is-active' : ''}`
            }
          >
            Start
          </NavLink>
          <NavLink
            to="/crawl/history"
            className={({ isActive }) =>
              `crawl-subnav-link${isActive ? ' is-active' : ''}`
            }
          >
            History
          </NavLink>
          <Link to="/" className="crawl-subnav-link crawl-subnav-quiet">
            Landing
          </Link>
        </nav>

        <div className="crawl-console-body">{children}</div>
      </div>
    </main>
  )
}
