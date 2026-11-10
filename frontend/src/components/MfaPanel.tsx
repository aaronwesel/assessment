import { useState, type FormEvent } from 'react'
import {
  confirmMfa,
  disableMfa,
  enrollMfa,
  fetchMfaStatus,
  verifyMfa,
} from '../api/auth'
import { useAuth } from '../auth/AuthContext'

/**
 * Simple MFA enroll / verify UI stub.
 * Does not render QR images — shows otpauth URI + secret for authenticator apps.
 * Never console.log secrets or backup codes in production builds.
 */
export function MfaPanel() {
  const { tokens } = useAuth()
  const access = tokens?.access_token
  const [enabled, setEnabled] = useState<boolean | null>(null)
  const [secret, setSecret] = useState('')
  const [otpauthUri, setOtpauthUri] = useState('')
  const [totpCode, setTotpCode] = useState('')
  const [backupCodes, setBackupCodes] = useState<string[] | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  if (!access) return null

  async function refreshStatus() {
    const status = await fetchMfaStatus(access!)
    setEnabled(status.mfa_enabled)
  }

  async function onEnroll() {
    setError(null)
    setMessage(null)
    setBusy(true)
    try {
      const material = await enrollMfa(access!)
      setSecret(material.secret)
      setOtpauthUri(material.otpauth_uri)
      setMessage('Scan or enter the secret in your authenticator, then confirm.')
      await refreshStatus()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Enroll failed')
    } finally {
      setBusy(false)
    }
  }

  async function onConfirm(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setMessage(null)
    setBusy(true)
    try {
      const result = await confirmMfa(access!, totpCode)
      setBackupCodes(result.backup_codes)
      setSecret('')
      setOtpauthUri('')
      setTotpCode('')
      setMessage('MFA enabled. Store backup codes offline — shown once.')
      await refreshStatus()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Confirm failed')
    } finally {
      setBusy(false)
    }
  }

  async function onVerify(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setMessage(null)
    setBusy(true)
    try {
      await verifyMfa(access!, { totpCode })
      setMessage('MFA code verified.')
      setTotpCode('')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Verify failed')
    } finally {
      setBusy(false)
    }
  }

  async function onDisable(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setMessage(null)
    setBusy(true)
    try {
      await disableMfa(access!, { totpCode })
      setBackupCodes(null)
      setMessage('MFA disabled.')
      setTotpCode('')
      await refreshStatus()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Disable failed')
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="mfa-panel" aria-labelledby="mfa-heading">
      <h2 id="mfa-heading">Multi-factor auth</h2>
      <p className="hint">
        TOTP stub for HIPAA-ready prep. Status:{' '}
        {enabled === null ? 'unknown' : enabled ? 'enabled' : 'disabled'}.
      </p>
      <div className="auth-mode-toggle" role="group" aria-label="MFA actions">
        <button type="button" className="btn btn-ghost" disabled={busy} onClick={() => void refreshStatus()}>
          Refresh status
        </button>
        <button type="button" className="btn" disabled={busy || enabled === true} onClick={() => void onEnroll()}>
          Enroll
        </button>
      </div>

      {otpauthUri ? (
        <div className="mfa-secret-block">
          <p className="hint">Authenticator URI (keep private):</p>
          <code className="mfa-code">{otpauthUri}</code>
          <p className="hint">Secret: {secret}</p>
        </div>
      ) : null}

      <form className="password-form" onSubmit={onConfirm}>
        <label>
          <span>TOTP code</span>
          <input
            inputMode="numeric"
            autoComplete="one-time-code"
            name="totp"
            value={totpCode}
            onChange={(e) => setTotpCode(e.target.value)}
            maxLength={16}
          />
        </label>
        <div className="auth-mode-toggle">
          <button type="submit" className="btn" disabled={busy || !totpCode}>
            Confirm enroll
          </button>
          <button type="button" className="btn btn-ghost" disabled={busy || !totpCode} onClick={(e) => void onVerify(e)}>
            Verify
          </button>
          <button type="button" className="btn btn-ghost" disabled={busy || !totpCode} onClick={(e) => void onDisable(e)}>
            Disable
          </button>
        </div>
      </form>

      {backupCodes ? (
        <div className="mfa-secret-block">
          <p className="hint">Backup codes (store offline; single-use):</p>
          <ul>
            {backupCodes.map((code) => (
              <li key={code}>
                <code>{code}</code>
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      {error ? <p className="form-error">{error}</p> : null}
      {message ? <p className="hint">{message}</p> : null}
    </section>
  )
}
