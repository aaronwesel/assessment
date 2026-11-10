import { hasGoogleClientId } from '../config'
import { useAuth } from '../auth/AuthContext'

/**
 * Placeholder for Google Identity Services One Tap.
 * Mount GIS script + initialize when VITE_GOOGLE_CLIENT_ID is set.
 */
export function GoogleOneTap() {
  const { login } = useAuth()
  const ready = hasGoogleClientId()

  return (
    <div className="social-slot">
      <button
        type="button"
        className="btn btn-social"
        disabled={!ready}
        onClick={() => {
          // Stub: real GIS One Tap will call backend token exchange.
          login({
            email: 'google.user@example.com',
            name: 'Google User',
            provider: 'google',
          })
        }}
      >
        Continue with Google
      </button>
      {!ready ? (
        <p className="hint">
          Set <code>VITE_GOOGLE_CLIENT_ID</code> to enable One Tap.
        </p>
      ) : (
        <p className="hint">One Tap mount ready (client ID present).</p>
      )}
    </div>
  )
}
