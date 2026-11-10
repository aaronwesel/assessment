/**
 * Optional MFA nudge for standard accounts; required setup gate for healthcare.
 */
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { fetchMfaStatus } from '../api/auth'
import { useAuth } from '../auth/AuthContext'

const DISMISS_KEY = 'assessment.mfa_prompt.dismissed'

export function MfaOnboardingBanner() {
  const { user, tokens } = useAuth()
  const access = tokens?.access_token
  const [mfaEnabled, setMfaEnabled] = useState<boolean | null>(user?.mfaEnabled ?? null)
  const [dismissed, setDismissed] = useState(() => sessionStorage.getItem(DISMISS_KEY) === '1')

  const policyRequired = user?.mfaPolicyRequired === true

  useEffect(() => {
    if (!access) return
    void fetchMfaStatus(access).then((status) => {
      setMfaEnabled(status.mfa_enabled)
    })
  }, [access])

  useEffect(() => {
    if (policyRequired) {
      sessionStorage.removeItem(DISMISS_KEY)
      setDismissed(false)
    }
  }, [policyRequired])

  if (!user || !access) return null
  if (mfaEnabled === true) return null
  if (!policyRequired && dismissed) return null

  function onSkip() {
    sessionStorage.setItem(DISMISS_KEY, '1')
    setDismissed(true)
  }

  if (policyRequired) {
    return (
      <section className="mfa-onboarding mfa-onboarding-required" aria-labelledby="mfa-required-heading">
        <h2 id="mfa-required-heading">Authenticator required</h2>
        <p className="lede">
          Healthcare accounts must enable multi-factor authentication before using the app.
          Scroll to <strong>Multi-factor auth</strong> below, tap <strong>Enroll</strong>, scan the QR code, then confirm with a 6-digit code.
        </p>
      </section>
    )
  }

  return (
    <section className="mfa-onboarding" aria-labelledby="mfa-optional-heading">
      <h2 id="mfa-optional-heading">Strengthen your account (optional)</h2>
      <p className="hint">
        Add an authenticator app when you are ready — you can skip and turn on MFA later from this page.
      </p>
      <div className="auth-mode-toggle">
        <button type="button" className="btn btn-ghost" onClick={onSkip}>
          Skip for now
        </button>
        <a className="btn" href="#mfa-heading">
          Set up MFA
        </a>
        <Link className="btn btn-ghost" to="/crawl">
          Continue to crawl
        </Link>
      </div>
    </section>
  )
}
