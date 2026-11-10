/**
 * Self-serve registration — POST /api/v1/auth/register then session hydrate.
 * Redirects authenticated users to /app.
 */
import { useState, type FormEvent } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { registerPassword } from '../api/auth'
import { useAuth } from '../auth/AuthContext'
import { AuthSocialStack } from '../components/AuthSocialStack'

export function RegisterPage() {
  const { isAuthenticated, loginWithTokens } = useAuth()
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [healthcareAccount, setHealthcareAccount] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  if (isAuthenticated) {
    return <Navigate to="/app" replace />
  }

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)

    const trimmedEmail = email.trim()
    if (!trimmedEmail || !password) {
      setError('Enter email and password.')
      return
    }
    if (password.length < 12) {
      setError(
        'Password must be at least 12 characters with upper, lower, digit, and special.',
      )
      return
    }

    setBusy(true)
    try {
      const tokens = await registerPassword({
        email: trimmedEmail,
        password,
        fullName: fullName.trim() || undefined,
        healthcareAccount,
      })
      await loginWithTokens(tokens, 'password')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Registration failed')
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="page register-page">
      <h1>Create account</h1>
      <p className="lede">
        Use Google, Microsoft, or email. Already have an account?{' '}
        <Link to="/login">Sign in</Link>.
      </p>

      <div className="auth-stack">
        <AuthSocialStack dividerLabel="or register with email" />
      </div>

      <form className="password-form" onSubmit={onSubmit} noValidate>
        <label className="field">
          <span>Name</span>
          <input
            type="text"
            name="full_name"
            autoComplete="name"
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            placeholder="Optional"
          />
        </label>
        <label className="field">
          <span>Email</span>
          <input
            type="email"
            name="email"
            autoComplete="username"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="you@example.com"
            required
          />
        </label>
        <label className="field">
          <span>Password</span>
          <input
            type="password"
            name="password"
            autoComplete="new-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            placeholder="At least 12 characters"
            required
          />
        </label>
        <label className="field field-checkbox">
          <input
            type="checkbox"
            name="healthcare_account"
            checked={healthcareAccount}
            onChange={(e) => setHealthcareAccount(e.target.checked)}
          />
          <span>
            Healthcare / HIPAA-minded organization (MFA required before using the app)
          </span>
        </label>
        {error ? <p className="form-error">{error}</p> : null}
        <button type="submit" className="btn btn-primary" disabled={busy}>
          {busy ? 'Creating…' : 'Create account'}
        </button>
      </form>
    </main>
  )
}
