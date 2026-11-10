/**
 * Frontend env config (Vite `import.meta.env`).
 *
 * Client IDs are public OAuth app identifiers (not secrets). API base points at
 * the FastAPI backend that verifies ID tokens and issues our JWTs.
 * Never put JWT secrets or AWS keys in VITE_* vars — those stay server-side.
 */
export const config = {
  googleClientId: import.meta.env.VITE_GOOGLE_CLIENT_ID ?? '',
  microsoftClientId: import.meta.env.VITE_MICROSOFT_CLIENT_ID ?? '',
  microsoftTenantId: import.meta.env.VITE_MICROSOFT_TENANT_ID ?? 'common',
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8000',
  /** Crawl.Api (.NET) — separate from FastAPI platform auth base. */
  crawlApiBaseUrl: import.meta.env.VITE_CRAWL_API_URL ?? 'http://localhost:5059',
  /** Optional pk_test_ hint; billing page also loads publishable key from the API. */
  stripePublishableKey: import.meta.env.VITE_STRIPE_PUBLISHABLE_KEY ?? '',
} as const

export function hasGoogleClientId(): boolean {
  return config.googleClientId.trim().length > 0
}

export function hasMicrosoftClientId(): boolean {
  return config.microsoftClientId.trim().length > 0
}
