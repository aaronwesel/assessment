/**
 * Public login page: Google One Tap, Microsoft MSAL, and password form.
 * Already-authenticated users are redirected home.
 */
import { Link, Navigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { AuthSocialStack } from '../components/AuthSocialStack'
import { PasswordForm } from '../components/PasswordForm'

export function LoginPage() {
  const { isAuthenticated } = useAuth()

  if (isAuthenticated) {
    return <Navigate to="/app" replace />
  }

  return (
    <main className="page login-page">
      <h1>Sign in</h1>
      <p className="lede">
        Use Google, Microsoft, or email and password. New here?{' '}
        <Link to="/register">Create an account</Link>.
      </p>

      <div className="auth-stack">
        <AuthSocialStack />
        <PasswordForm />
      </div>
    </main>
  )
}
