import { useAuth } from '../auth/AuthContext'

/** Authenticated home — shows identity from /auth/me; no domain screens yet. */
export function HomePage() {
  const { user } = useAuth()

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
      <p className="hint">Product screens land after the assessment prompt.</p>
    </main>
  )
}
