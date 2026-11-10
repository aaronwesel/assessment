/**
 * Superadmin console shell — nav + outlet for dashboard widgets.
 */
import { NavLink, Outlet } from 'react-router-dom'

const links = [
  { to: '/superadmin', end: true, label: 'Overview' },
  { to: '/superadmin/health', end: false, label: 'Health / SLO' },
  { to: '/superadmin/flags', end: false, label: 'Flags' },
  { to: '/superadmin/audit', end: false, label: 'Audit skim' },
  { to: '/superadmin/break-glass', end: false, label: 'Break-glass' },
  { to: '/superadmin/webhooks', end: false, label: 'Webhooks' },
  { to: '/superadmin/billing', end: false, label: 'Billing skim' },
]

export function SuperadminLayout() {
  return (
    <div className="sa-shell">
      <aside className="sa-nav" aria-label="Superadmin">
        <p className="sa-nav-title">Superadmin</p>
        <nav className="sa-nav-list">
          {links.map((link) => (
            <NavLink
              key={link.to}
              to={link.to}
              end={link.end}
              className={({ isActive }) =>
                `sa-nav-link${isActive ? ' is-active' : ''}`
              }
            >
              {link.label}
            </NavLink>
          ))}
        </nav>
      </aside>
      <div className="sa-main">
        <Outlet />
      </div>
    </div>
  )
}
