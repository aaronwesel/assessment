/**
 * Microsoft Entra / MSAL popup sign-in.
 *
 * Uses `@azure/msal-browser` with sessionStorage cache, then exchanges the ID
 * token with POST /api/v1/auth/microsoft/onetap. Backend verifies via JWKS.
 */
import { useState } from 'react'
import {
  PublicClientApplication,
  type AuthenticationResult,
} from '@azure/msal-browser'
import { exchangeOneTap } from '../api/auth'
import { useAuth } from '../auth/AuthContext'
import { config, hasMicrosoftClientId } from '../config'

let msalInstance: PublicClientApplication | null = null

async function getMsal(): Promise<PublicClientApplication> {
  if (msalInstance) return msalInstance
  msalInstance = new PublicClientApplication({
    auth: {
      clientId: config.microsoftClientId,
      authority: `https://login.microsoftonline.com/${config.microsoftTenantId}`,
      redirectUri: window.location.origin,
    },
    cache: {
      cacheLocation: 'sessionStorage',
    },
  })
  await msalInstance.initialize()
  return msalInstance
}

/**
 * Microsoft Entra / MSAL popup (redirect-compatible config).
 * Exchanges the ID token with POST /api/v1/auth/microsoft/onetap.
 */
export function MicrosoftSignIn() {
  const { loginWithTokens } = useAuth()
  const ready = hasMicrosoftClientId()
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function onClick() {
    if (!ready || busy) return
    setBusy(true)
    setError(null)
    try {
      const pca = await getMsal()
      const result: AuthenticationResult = await pca.loginPopup({
        scopes: ['openid', 'profile', 'email'],
      })
      const idToken = result.idToken
      if (!idToken) {
        throw new Error('Microsoft did not return an ID token')
      }
      const tokens = await exchangeOneTap('microsoft', idToken)
      await loginWithTokens(tokens, 'microsoft')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Microsoft sign-in failed')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="social-slot">
      <button
        type="button"
        className="btn btn-social"
        disabled={!ready || busy}
        onClick={() => {
          void onClick()
        }}
      >
        {busy ? 'Signing in…' : 'Continue with Microsoft'}
      </button>
      {!ready ? (
        <p className="hint">
          Set <code>VITE_MICROSOFT_CLIENT_ID</code> to enable Microsoft sign-in.
        </p>
      ) : null}
      {error ? <p className="form-error">{error}</p> : null}
    </div>
  )
}
