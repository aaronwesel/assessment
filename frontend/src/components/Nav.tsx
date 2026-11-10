import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export function Nav() {
  const { user, isAuthenticated, logout } = useAuth()

  return (
    <header className="nav">
      <Link to={isAuthenticated ? '/' : '/login'} className="nav-brand">
        Assessment
      </Link>
      {isAuthenticated ? (
        <div className="nav-actions">
          <span className="nav-user">{user?.email}</span>
          <button type="button" className="btn btn-ghost" onClick={logout}>
            Sign out
          </button>
        </div>
      ) : null}
    </header>
  )
}
