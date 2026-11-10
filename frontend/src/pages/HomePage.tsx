import { Link } from 'react-router-dom'
import { MfaPanel } from '../components/MfaPanel'
import { useAuth } from '../auth/AuthContext'

/** Authenticated home — shows identity from /auth/me; MFA enroll/verify stub. */
export function HomePage() {
  const { user } = useAuth()
  const isAdmin =
    user?.roles?.includes('admin') || user?.roles?.includes('superadmin')

  return (
    <main className="page home-page">
      <h1>Home</h1>
      <p className="lede">
        Signed in as {user?.email}
        {user?.name ? ` (${user.name})` : ''}.
      </p>
      <dl className="home-meta">
        <div>
          <dt>Provider</dt>
          <dd>{user?.provider ?? '—'}</dd>
        </div>
        <div>
          <dt>Roles</dt>
          <dd>{user?.roles?.join(', ') || '—'}</dd>
        </div>
      </dl>
      {isAdmin ? (
        <p className="hint">
          Platform admin: open the <Link to="/superadmin">superadmin console</Link>.
        </p>
      ) : null}
      <MfaPanel />
      <p className="hint">
        Open the <Link to="/crawl">crawl console</Link> to start a job or view{' '}
        <Link to="/crawl/history">history</Link>.
      </p>
    </main>
  )
}
