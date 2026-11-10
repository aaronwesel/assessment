/**
 * React auth context: JWT session in sessionStorage + /auth/me hydration.
 *
 * Why sessionStorage (not localStorage): tokens clear when the tab closes,
 * reducing XSS persistence risk on shared machines. Still not a substitute for
 * HttpOnly cookies in a full production hardening pass — assessment scaffold.
 *
 * loginWithTokens: after any provider returns a TokenPair, fetch `/me` and persist.
 * logout: best-effort API revoke (Redis blacklist) then clear local session.
 */
import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import { fetchMe, logoutTokens } from '../api/auth'
import type { AuthContextValue, AuthProvider, AuthTokens, AuthUser } from './types'

const AuthContext = createContext<AuthContextValue | null>(null)

const USER_KEY = 'assessment.auth.user'
const TOKENS_KEY = 'assessment.auth.tokens'

function readStoredUser(): AuthUser | null {
  try {
    const raw = sessionStorage.getItem(USER_KEY)
    if (!raw) return null
    return JSON.parse(raw) as AuthUser
  } catch {
    return null
  }
}

function readStoredTokens(): AuthTokens | null {
  try {
    const raw = sessionStorage.getItem(TOKENS_KEY)
    if (!raw) return null
    return JSON.parse(raw) as AuthTokens
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => readStoredUser())
  const [tokens, setTokens] = useState<AuthTokens | null>(() => readStoredTokens())

  const loginWithTokens = useCallback(
    async (nextTokens: AuthTokens, provider: AuthProvider) => {
      const nextUser = await fetchMe(nextTokens.access_token, provider)
      sessionStorage.setItem(TOKENS_KEY, JSON.stringify(nextTokens))
      sessionStorage.setItem(USER_KEY, JSON.stringify(nextUser))
      setTokens(nextTokens)
      setUser(nextUser)
    },
    [],
  )

  const logout = useCallback(async () => {
    const current = tokens ?? readStoredTokens()
    if (current) {
      await logoutTokens(current)
    }
    sessionStorage.removeItem(TOKENS_KEY)
    sessionStorage.removeItem(USER_KEY)
    setTokens(null)
    setUser(null)
  }, [tokens])

  const value = useMemo(
    () => ({
      user,
      tokens,
      isAuthenticated: user !== null && tokens !== null,
      loginWithTokens,
      logout,
    }),
    [user, tokens, loginWithTokens, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) {
    throw new Error('useAuth must be used within AuthProvider')
  }
  return ctx
}
