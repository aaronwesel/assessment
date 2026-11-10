/**
 * Optional browser geolocation (or IP fallback) + Open-Meteo weather for a friendly welcome line.
 * Fails silently to a generic greeting when APIs or permissions are unavailable.
 */
import { useEffect, useState } from 'react'

type GreetingState =
  | { kind: 'loading' }
  | { kind: 'weather'; city: string; phrase: string }
  | { kind: 'plain' }

type Coords = { latitude: number; longitude: number; city?: string }

const GEO_TIMEOUT_MS = 6_000

function weatherPhrase(code: number, isDay: number): string {
  if (code === 0) return isDay ? 'nice and sunny' : 'clear and calm'
  if (code === 1 || code === 2) return 'pleasant and bright'
  if (code === 3) return 'a bit overcast but cozy'
  if (code === 45 || code === 48) return 'misty'
  if ((code >= 51 && code <= 67) || (code >= 80 && code <= 82)) return 'a little rainy'
  if ((code >= 71 && code <= 77) || (code >= 85 && code <= 86)) return 'snowy'
  if (code >= 95) return 'stormy — stay dry'
  return 'pleasant'
}

async function reverseCity(latitude: number, longitude: number): Promise<string | undefined> {
  try {
    const url = new URL('https://geocoding-api.open-meteo.com/v1/reverse')
    url.searchParams.set('latitude', String(latitude))
    url.searchParams.set('longitude', String(longitude))
    url.searchParams.set('language', 'en')
    url.searchParams.set('count', '1')
    const res = await fetch(url)
    if (!res.ok) return undefined
    const data = (await res.json()) as {
      results?: { name?: string; admin1?: string }[]
    }
    const hit = data.results?.[0]
    if (!hit?.name) return undefined
    return hit.admin1 ? `${hit.name}, ${hit.admin1}` : hit.name
  } catch {
    return undefined
  }
}

async function fetchWeather(latitude: number, longitude: number): Promise<{ phrase: string } | null> {
  try {
    const url = new URL('https://api.open-meteo.com/v1/forecast')
    url.searchParams.set('latitude', String(latitude))
    url.searchParams.set('longitude', String(longitude))
    url.searchParams.set('current', 'weather_code,is_day')
    const res = await fetch(url)
    if (!res.ok) return null
    const data = (await res.json()) as {
      current?: { weather_code?: number; is_day?: number }
    }
    const code = data.current?.weather_code
    const isDay = data.current?.is_day ?? 1
    if (code == null) return null
    return { phrase: weatherPhrase(code, isDay) }
  } catch {
    return null
  }
}

function browserCoords(): Promise<Coords | null> {
  if (typeof navigator === 'undefined' || !navigator.geolocation) {
    return Promise.resolve(null)
  }
  return new Promise((resolve) => {
    const timer = window.setTimeout(() => resolve(null), GEO_TIMEOUT_MS)
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        window.clearTimeout(timer)
        resolve({
          latitude: pos.coords.latitude,
          longitude: pos.coords.longitude,
        })
      },
      () => {
        window.clearTimeout(timer)
        resolve(null)
      },
      { enableHighAccuracy: false, maximumAge: 600_000, timeout: GEO_TIMEOUT_MS },
    )
  })
}

async function ipCoords(): Promise<Coords | null> {
  try {
    const res = await fetch('https://ipwho.is/')
    if (!res.ok) return null
    const data = (await res.json()) as {
      success?: boolean
      latitude?: number
      longitude?: number
      city?: string
    }
    if (!data.success || data.latitude == null || data.longitude == null) return null
    return {
      latitude: data.latitude,
      longitude: data.longitude,
      city: data.city || undefined,
    }
  } catch {
    return null
  }
}

async function resolveCoords(): Promise<Coords | null> {
  const fromBrowser = await browserCoords()
  if (fromBrowser) return fromBrowser
  return ipCoords()
}

export function WelcomeGeoGreeting() {
  const [state, setState] = useState<GreetingState>({ kind: 'loading' })

  useEffect(() => {
    let cancelled = false

    void (async () => {
      const coords = await resolveCoords()
      if (cancelled) return
      if (!coords) {
        setState({ kind: 'plain' })
        return
      }

      const city =
        coords.city ??
        (await reverseCity(coords.latitude, coords.longitude)) ??
        'your area'

      const weather = await fetchWeather(coords.latitude, coords.longitude)
      if (cancelled) return

      if (!weather) {
        setState({ kind: 'plain' })
        return
      }

      setState({ kind: 'weather', city, phrase: weather.phrase })
    })()

    return () => {
      cancelled = true
    }
  }, [])

  if (state.kind === 'loading') {
    return (
      <p className="crawl-welcome-greeting crawl-welcome-greeting--loading" aria-live="polite">
        Welcome — getting set up for you…
      </p>
    )
  }

  if (state.kind === 'weather') {
    return (
      <p className="crawl-welcome-greeting" aria-live="polite">
        Welcome — looks <em>{state.phrase}</em> in your area of <strong>{state.city}</strong> today.
      </p>
    )
  }

  return (
    <p className="crawl-welcome-greeting" aria-live="polite">
      Welcome — glad you&apos;re here. Let&apos;s map your site step by step.
    </p>
  )
}
