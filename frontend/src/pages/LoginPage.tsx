/**
 * Public login page: Google One Tap, Microsoft MSAL, and password form.
 * Already-authenticated users are redirected home.
 */
import { Navigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { GoogleOneTap } from '../components/GoogleOneTap'
import { MicrosoftSignIn } from '../components/MicrosoftSignIn'
import { PasswordForm } from '../components/PasswordForm'

export function LoginPage() {
  const { isAuthenticated } = useAuth()

  if (isAuthenticated) {
    return <Navigate to="/" replace />
  }

  return (
    <main className="page login-page">
      <h1>Sign in</h1>
      <p className="lede">Use Google, Microsoft, or email and password.</p>

      <div className="auth-stack">
        <GoogleOneTap />
        <MicrosoftSignIn />
        <div className="divider" role="separator">
          <span>or</span>
        </div>
        <PasswordForm />
      </div>
    </main>
  )
}
