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
}): Promise<AuthTokens> {
  const response = await fetch(apiUrl('/api/v1/auth/register'), {
    method: 'POST',
    headers: jsonHeaders,
    body: JSON.stringify({
      email: input.email,
      password: input.password,
      full_name: input.fullName || null,
    }),
  })
  if (!response.ok) throw new Error(await readErrorMessage(response))
  return response.json() as Promise<AuthTokens>
}

export async function loginPassword(input: {
  email: string
  password: string
}): Promise<AuthTokens> {
  const response = await fetch(apiUrl('/api/v1/auth/login'), {
    method: 'POST',
    headers: jsonHeaders,
    body: JSON.stringify(input),
  })
  if (!response.ok) throw new Error(await readErrorMessage(response))
  return response.json() as Promise<AuthTokens>
}

export async function exchangeOneTap(
  provider: 'google' | 'microsoft',
  idToken: string,
): Promise<AuthTokens> {
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
  }
  return {
    id: body.subject,
    email: body.email ?? body.subject,
    name: body.full_name ?? body.email ?? 'User',
    roles: body.roles,
    provider,
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
