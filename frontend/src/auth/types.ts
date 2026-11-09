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
}

export type AuthContextValue = {
  user: AuthUser | null
  tokens: AuthTokens | null
  isAuthenticated: boolean
  loginWithTokens: (tokens: AuthTokens, provider: AuthProvider) => Promise<void>
  logout: () => Promise<void>
}
