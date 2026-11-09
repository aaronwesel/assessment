export type AuthUser = {
  email: string
  name: string
  provider: 'password' | 'google' | 'microsoft'
}

export type AuthContextValue = {
  user: AuthUser | null
  isAuthenticated: boolean
  login: (user: AuthUser) => void
  logout: () => void
}
