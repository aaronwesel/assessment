import { useEffect, useRef, useState } from 'react'
import { exchangeOneTap } from '../api/auth'
import { useAuth } from '../auth/AuthContext'
import { config, hasGoogleClientId } from '../config'

const GIS_SCRIPT_ID = 'google-identity-services'
const GIS_SRC = 'https://accounts.google.com/gsi/client'

function loadGisScript(): Promise<void> {
  if (window.google?.accounts?.id) return Promise.resolve()
  const existing = document.getElementById(GIS_SCRIPT_ID)
  if (existing) {
    return new Promise((resolve, reject) => {
      existing.addEventListener('load', () => resolve())
      existing.addEventListener('error', () => reject(new Error('GIS script failed')))
    })
  }
  return new Promise((resolve, reject) => {
    const script = document.createElement('script')
    script.id = GIS_SCRIPT_ID
    script.src = GIS_SRC
    script.async = true
    script.onload = () => resolve()
    script.onerror = () => reject(new Error('GIS script failed'))
    document.head.appendChild(script)
  })
}

/**
 * Google Identity Services One Tap + button.
 * Exchanges the GIS credential JWT with POST /api/v1/auth/google/onetap.
 */
export function GoogleOneTap() {
  const { loginWithTokens } = useAuth()
  const ready = hasGoogleClientId()
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const buttonRef = useRef<HTMLDivElement>(null)
  const initialized = useRef(false)

  useEffect(() => {
    if (!ready || initialized.current) return
    let cancelled = false

    ;(async () => {
      try {
        await loadGisScript()
        if (cancelled || !window.google?.accounts?.id) return
        initialized.current = true
        window.google.accounts.id.initialize({
          client_id: config.googleClientId,
          callback: async (response) => {
            if (!response.credential) {
              setError('Google did not return a credential.')
              return
            }
            setBusy(true)
            setError(null)
            try {
              const tokens = await exchangeOneTap('google', response.credential)
              await loginWithTokens(tokens, 'google')
            } catch (err) {
              setError(err instanceof Error ? err.message : 'Google sign-in failed')
            } finally {
              setBusy(false)
            }
          },
          auto_select: false,
          cancel_on_tap_outside: true,
        })
        window.google.accounts.id.prompt()
        if (buttonRef.current) {
          window.google.accounts.id.renderButton(buttonRef.current, {
            theme: 'outline',
            size: 'large',
            width: 320,
            text: 'continue_with',
            shape: 'rectangular',
          })
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Failed to load Google One Tap')
        }
      }
    })()

    return () => {
      cancelled = true
    }
  }, [ready, loginWithTokens])

  return (
    <div className="social-slot">
      {!ready ? (
        <>
          <button type="button" className="btn btn-social" disabled>
            Continue with Google
          </button>
          <p className="hint">
            Set <code>VITE_GOOGLE_CLIENT_ID</code> to enable One Tap.
          </p>
        </>
      ) : (
        <>
          <div ref={buttonRef} aria-busy={busy} />
          {busy ? <p className="hint">Signing in…</p> : null}
          {error ? <p className="form-error">{error}</p> : null}
        </>
      )}
    </div>
  )
}
