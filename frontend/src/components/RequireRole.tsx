/**
 * Role gate for admin / superadmin surfaces.
 * Unauthenticated → /login; authenticated but wrong role → /app.
 */
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import type { ReactNode } from 'react'

const DEFAULT_ALLOWED = ['admin', 'superadmin']

export function RequireRole({
  children,
  allowedRoles = DEFAULT_ALLOWED,
}: {
  children: ReactNode
  allowedRoles?: string[]
}) {
  const { isAuthenticated, user } = useAuth()
  const location = useLocation()

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />
  }

  const roles = user?.roles ?? []
  const ok = roles.some((role) => allowedRoles.includes(role))
  if (!ok) {
    return <Navigate to="/app" replace />
  }

  return children
}
