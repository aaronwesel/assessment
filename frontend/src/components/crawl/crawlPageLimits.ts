/** Mirrors Crawl.Domain.Jobs.CrawlJob page budget constants. */
export const DEFAULT_MAX_PAGES = 200
export const ABSOLUTE_MAX_PAGES = 5000

export function clampMaxPages(value: number): number {
  if (!Number.isFinite(value)) return DEFAULT_MAX_PAGES
  return Math.min(ABSOLUTE_MAX_PAGES, Math.max(1, Math.round(value)))
}

export function parseMaxPagesInput(raw: string): number | undefined {
  const trimmed = raw.trim()
  if (!trimmed) return undefined
  const n = Number(trimmed)
  if (!Number.isFinite(n)) return undefined
  return clampMaxPages(n)
}
