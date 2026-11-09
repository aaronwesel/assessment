import { useState, type FormEvent } from 'react'
import { loginPassword, registerPassword } from '../api/auth'
import { useAuth } from '../auth/AuthContext'

/** Email/password register + login against FastAPI JWT endpoints. */
export function PasswordForm() {
  const { loginWithTokens } = useAuth()
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [fullName, setFullName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)

    const trimmed = email.trim()
    if (!trimmed || !password) {
      setError('Enter email and password.')
      return
    }
    if (mode === 'register' && password.length < 8) {
      setError('Password must be at least 8 characters.')
      return
    }

    setBusy(true)
    try {
      const tokens =
        mode === 'register'
          ? await registerPassword({
              email: trimmed,
              password,
              fullName: fullName.trim() || undefined,
            })
          : await loginPassword({ email: trimmed, password })
      await loginWithTokens(tokens, 'password')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Sign-in failed')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="password-form" onSubmit={onSubmit} noValidate>
      <div className="auth-mode-toggle" role="group" aria-label="Password mode">
        <button
          type="button"
          className={`btn btn-ghost ${mode === 'login' ? 'is-active' : ''}`}
          onClick={() => setMode('login')}
          disabled={busy}
        >
          Sign in
        </button>
        <button
          type="button"
          className={`btn btn-ghost ${mode === 'register' ? 'is-active' : ''}`}
          onClick={() => setMode('register')}
          disabled={busy}
        >
          Register
        </button>
      </div>

      {mode === 'register' ? (
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
      ) : null}

      <label className="field">
        <span>Email</span>
        <input
          type="email"
          name="email"
          autoComplete="username"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="you@example.com"
        />
      </label>
      <label className="field">
        <span>Password</span>
        <input
          type="password"
          name="password"
          autoComplete={mode === 'register' ? 'new-password' : 'current-password'}
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          placeholder="••••••••"
        />
      </label>
      {error ? <p className="form-error">{error}</p> : null}
      <button type="submit" className="btn btn-primary" disabled={busy}>
        {busy ? 'Please wait…' : mode === 'register' ? 'Create account' : 'Sign in'}
      </button>
    </form>
  )
}
