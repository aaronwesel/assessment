import { useState, type FormEvent } from 'react'
import { useAuth } from '../auth/AuthContext'

/** Password login stub — posts to backend auth when API is wired. */
export function PasswordForm() {
  const { login } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)

    const trimmed = email.trim()
    if (!trimmed || !password) {
      setError('Enter email and password.')
      return
    }

    // Stub: replace with POST /auth/login → JWT session.
    login({
      email: trimmed,
      name: trimmed.split('@')[0] || 'User',
      provider: 'password',
    })
  }

  return (
    <form className="password-form" onSubmit={onSubmit} noValidate>
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
          autoComplete="current-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          placeholder="••••••••"
        />
      </label>
      {error ? <p className="form-error">{error}</p> : null}
      <button type="submit" className="btn btn-primary">
        Sign in
      </button>
    </form>
  )
}
