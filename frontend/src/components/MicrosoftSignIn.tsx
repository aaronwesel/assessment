import { hasMicrosoftClientId } from '../config'
import { useAuth } from '../auth/AuthContext'

/**
 * Placeholder for Microsoft / MSAL One Tap (or popup) button.
 * Wire MSAL when VITE_MICROSOFT_CLIENT_ID is set.
 */
export function MicrosoftSignIn() {
  const { login } = useAuth()
  const ready = hasMicrosoftClientId()

  return (
    <div className="social-slot">
      <button
        type="button"
        className="btn btn-social"
        disabled={!ready}
        onClick={() => {
          // Stub: real MSAL flow will call backend token exchange.
          login({
            email: 'microsoft.user@example.com',
            name: 'Microsoft User',
            provider: 'microsoft',
          })
        }}
      >
        Continue with Microsoft
      </button>
      {!ready ? (
        <p className="hint">
          Set <code>VITE_MICROSOFT_CLIENT_ID</code> to enable Microsoft sign-in.
        </p>
      ) : (
        <p className="hint">Microsoft button ready (client ID present).</p>
      )}
    </div>
  )
}
