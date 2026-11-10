/**
 * Wizard-only advanced options (API accepts depth + maxPages; rest are operator notes / UI prefs).
 */
export type SelectorMode = 'css' | 'xpath'

export type CrawlAdvancedOptions = {
  maxDepth: number
  maxPages: number
  politenessNote: string
  honorRobots: boolean
  credentialVaultRef: string
  selectorMode: SelectorMode
  elementPickGoal: string
}

export const DEFAULT_ADVANCED: CrawlAdvancedOptions = {
  maxDepth: 2,
  maxPages: 200,
  politenessNote: 'Default worker delay (~1s/host) — increase if the site rate-limits you.',
  honorRobots: true,
  credentialVaultRef: '',
  selectorMode: 'css',
  elementPickGoal: '',
}

const STORAGE_KEY = 'trailmap.crawl.advanced'

export function loadAdvancedFromStorage(): CrawlAdvancedOptions {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return { ...DEFAULT_ADVANCED }
    return { ...DEFAULT_ADVANCED, ...(JSON.parse(raw) as Partial<CrawlAdvancedOptions>) }
  } catch {
    return { ...DEFAULT_ADVANCED }
  }
}

export function saveAdvancedToStorage(opts: CrawlAdvancedOptions) {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(opts))
}

export function parseAdvancedFromSearch(params: URLSearchParams): Partial<CrawlAdvancedOptions> {
  const next: Partial<CrawlAdvancedOptions> = {}
  const depth = params.get('depth')
  if (depth) next.maxDepth = Number(depth)
  const pages = params.get('maxPages')
  if (pages) next.maxPages = Number(pages)
  const sel = params.get('selectorMode')
  if (sel === 'css' || sel === 'xpath') next.selectorMode = sel
  if (params.get('advanced') === '1') {
    const stored = loadAdvancedFromStorage()
    return { ...stored, ...next }
  }
  return next
}
