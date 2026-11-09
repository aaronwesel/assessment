export const config = {
  googleClientId: import.meta.env.VITE_GOOGLE_CLIENT_ID ?? '',
  microsoftClientId: import.meta.env.VITE_MICROSOFT_CLIENT_ID ?? '',
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8000',
} as const

export function hasGoogleClientId(): boolean {
  return config.googleClientId.trim().length > 0
}

export function hasMicrosoftClientId(): boolean {
  return config.microsoftClientId.trim().length > 0
}
