/**
 * Shared auth types for the SPA.
 *
 * `AuthTokens` mirrors the FastAPI TokenPair (access + refresh JWTs).
 * `AuthUser` is the hydrated `/auth/me` profile shown after login.
 * Provider tracks which login path minted the current session.
 */
export type AuthProvider = 'password' | 'google' | 'microsoft'

export type AuthTokens = {
  access_token: string
  refresh_token: string
  token_type?: string
}

export type AuthUser = {
  id: string
  email: string
  name: string
  roles: string[]
  provider: AuthProvider
  mfaEnabled?: boolean
  mfaPolicyRequired?: boolean
}

export type AuthContextValue = {
  user: AuthUser | null
  tokens: AuthTokens | null
  isAuthenticated: boolean
  loginWithTokens: (tokens: AuthTokens, provider: AuthProvider) => Promise<void>
  logout: () => Promise<void>
}
