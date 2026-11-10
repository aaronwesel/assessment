/**
 * Thin fetch client for FastAPI auth routes under `/api/v1/auth/*`.
 *
 * Flow overview:
 * - Password register/login → JWT pair
 * - Google/Microsoft One Tap → POST id_token → JWT pair
 * - `/me` hydrates the user for AuthContext (Bearer access token)
 * - Logout best-effort revokes tokens via Redis blacklist on the API
 *
 * Do not console.log tokens, passwords, or ID tokens (HIPAA / PII hygiene).
 */
import { config } from '../config'
import type { AuthProvider, AuthTokens, AuthUser } from '../auth/types'

const jsonHeaders = { 'Content-Type': 'application/json' }

function apiUrl(path: string): string {
  const base = config.apiBaseUrl.replace(/\/$/, '')
  return `${base}${path}`
}

async function readErrorMessage(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as { detail?: string }
    if (typeof body.detail === 'string') return body.detail
  } catch {
    /* ignore */
  }
  return `Request failed (${response.status})`
}

export type ProvidersStatus = {
  google: boolean
  microsoft: boolean
  password: boolean
  ses_enabled: boolean
  login_alert_email_enabled: boolean
  mfa_available?: boolean
}

export async function fetchProviders(): Promise<ProvidersStatus> {
  const response = await fetch(apiUrl('/api/v1/auth/providers'))
  if (!response.ok) throw new Error(await readErrorMessage(response))
  return response.json() as Promise<ProvidersStatus>
}

export async function registerPassword(input: {
  email: string
  password: string
  fullName?: string
  healthcareAccount?: boolean
}): Promise<AuthTokens> {
  const response = await fetch(apiUrl('/api/v1/auth/register'), {
    method: 'POST',
    headers: jsonHeaders,
    body: JSON.stringify({
      email: input.email,
      password: input.password,
      full_name: input.fullName || null,
      healthcare_account: Boolean(input.healthcareAccount),
    }),
  })
  if (!response.ok) throw new Error(await readErrorMessage(response))
  return response.json() as Promise<AuthTokens>
}

export async function loginPassword(input: {
  email: string
  password: string
  totpCode?: string
  backupCode?: string
}): Promise<AuthTokens> {
  const response = await fetch(apiUrl('/api/v1/auth/login'), {
    method: 'POST',
    headers: jsonHeaders,
    body: JSON.stringify({
      email: input.email,
      password: input.password,
      totp_code: input.totpCode || null,
      backup_code: input.backupCode || null,
    }),
  })
  if (!response.ok) throw new Error(await readErrorMessage(response))
  return response.json() as Promise<AuthTokens>
}

export async function fetchMfaStatus(
  accessToken: string,
): Promise<{ mfa_enabled: boolean; mfa_policy_required?: boolean }> {
  const response = await fetch(apiUrl('/api/v1/auth/mfa/status'), {
    headers: { Authorization: `Bearer ${accessToken}` },
  })
  if (!response.ok) throw new Error(await readErrorMessage(response))
  return response.json() as Promise<{ mfa_enabled: boolean }>
}

export async function enrollMfa(
  accessToken: string,
): Promise<{ secret: string; otpauth_uri: string }> {
  const response = await fetch(apiUrl('/api/v1/auth/mfa/enroll'), {
    method: 'POST',
    headers: { Authorization: `Bearer ${accessToken}`, ...jsonHeaders },
  })
  if (!response.ok) throw new Error(await readErrorMessage(response))
  return response.json() as Promise<{ secret: string; otpauth_uri: string }>
}

export async function confirmMfa(
  accessToken: string,
  totpCode: string,
): Promise<{ mfa_enabled: boolean; backup_codes: string[] }> {
  const response = await fetch(apiUrl('/api/v1/auth/mfa/confirm'), {
    method: 'POST',
    headers: { Authorization: `Bearer ${accessToken}`, ...jsonHeaders },
    body: JSON.stringify({ totp_code: totpCode }),
  })
  if (!response.ok) throw new Error(await readErrorMessage(response))
  return response.json() as Promise<{ mfa_enabled: boolean; backup_codes: string[] }>
}

export async function verifyMfa(
  accessToken: string,
  input: { totpCode?: string; backupCode?: string },
): Promise<{ ok: boolean }> {
  const response = await fetch(apiUrl('/api/v1/auth/mfa/verify'), {
    method: 'POST',
    headers: { Authorization: `Bearer ${accessToken}`, ...jsonHeaders },
    body: JSON.stringify({
      totp_code: input.totpCode || null,
      backup_code: input.backupCode || null,
    }),
  })
  if (!response.ok) throw new Error(await readErrorMessage(response))
  return response.json() as Promise<{ ok: boolean }>
}

export async function disableMfa(
  accessToken: string,
  input: { totpCode?: string; backupCode?: string },
): Promise<void> {
  const response = await fetch(apiUrl('/api/v1/auth/mfa/disable'), {
    method: 'POST',
    headers: { Authorization: `Bearer ${accessToken}`, ...jsonHeaders },
    body: JSON.stringify({
      totp_code: input.totpCode || null,
      backup_code: input.backupCode || null,
    }),
  })
  if (!response.ok) throw new Error(await readErrorMessage(response))
}

export async function exchangeOneTap(
  provider: 'google' | 'microsoft',
  idToken: string,
): Promise<AuthTokens> {
  /** Hand the IdP JWT to the API; server verifies and returns our access/refresh pair. */
  const path =
    provider === 'google'
      ? '/api/v1/auth/google/onetap'
      : '/api/v1/auth/microsoft/onetap'
  const response = await fetch(apiUrl(path), {
    method: 'POST',
    headers: jsonHeaders,
    body: JSON.stringify({ id_token: idToken, provider }),
  })
  if (!response.ok) throw new Error(await readErrorMessage(response))
  return response.json() as Promise<AuthTokens>
}

export async function fetchMe(
  accessToken: string,
  provider: AuthProvider,
): Promise<AuthUser> {
  const response = await fetch(apiUrl('/api/v1/auth/me'), {
    headers: { Authorization: `Bearer ${accessToken}` },
  })
  if (!response.ok) throw new Error(await readErrorMessage(response))
  const body = (await response.json()) as {
    subject: string
    email?: string | null
    full_name?: string | null
    roles: string[]
    mfa_enabled?: boolean
    mfa_policy_required?: boolean
  }
  return {
    id: body.subject,
    email: body.email ?? body.subject,
    name: body.full_name ?? body.email ?? 'User',
    roles: body.roles,
    provider,
    mfaEnabled: Boolean(body.mfa_enabled),
    mfaPolicyRequired: Boolean(body.mfa_policy_required),
  }
}

export async function logoutTokens(tokens: AuthTokens): Promise<void> {
  try {
    await fetch(apiUrl('/api/v1/auth/logout'), {
      method: 'POST',
      headers: jsonHeaders,
      body: JSON.stringify({
        access_token: tokens.access_token,
        refresh_token: tokens.refresh_token,
      }),
    })
  } catch {
    /* best-effort revoke */
  }
}
