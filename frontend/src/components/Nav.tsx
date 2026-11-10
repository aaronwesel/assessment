/**
 * Top nav: brand link + auth actions.
 * Superadmin link appears only for admin / superadmin roles.
 */
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

function isPlatformAdmin(roles: string[] | undefined): boolean {
  if (!roles) return false
  return roles.includes('admin') || roles.includes('superadmin')
}

export function Nav() {
  const { user, isAuthenticated, logout } = useAuth()

  return (
    <header className="nav">
      <Link to="/" className="nav-brand">
        Assessment
      </Link>
      <div className="nav-actions">
        {isAuthenticated ? (
          <>
            <Link className="nav-link" to="/app">
              App
            </Link>
            {isPlatformAdmin(user?.roles) ? (
              <Link className="nav-link" to="/superadmin">
                Superadmin
              </Link>
            ) : null}
            <span className="nav-user">{user?.email}</span>
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => {
                void logout()
              }}
            >
              Sign out
            </button>
          </>
        ) : (
          <>
            <Link className="nav-link" to="/login">
              Sign in
            </Link>
            <Link className="btn btn-primary nav-cta" to="/register">
              Register
            </Link>
          </>
        )}
      </div>
    </header>
  )
}
