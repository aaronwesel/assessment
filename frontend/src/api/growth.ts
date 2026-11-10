/**
 * Growth / pricing helpers (FastAPI) — public config for landing + wizard.
 */
import { config } from '../config'

export type CrawlPricing = {
  price_per_page_cents: number
  currency: string
  display_per_page: string
  free_pages_allowance: number
  billable_unit: string
  stripe_metered_configured: boolean
  anonymous_crawl_allowed: boolean
  save_jobs_requires_auth: boolean
}

export async function fetchCrawlPricing(): Promise<CrawlPricing> {
  const base = config.apiBaseUrl.replace(/\/$/, '')
  const res = await fetch(`${base}/api/v1/growth/crawl-pricing`)
  if (!res.ok) {
    return {
      price_per_page_cents: 2,
      currency: 'usd',
      display_per_page: '$0.02',
      free_pages_allowance: 5000,
      billable_unit: 'pages_with_results',
      stripe_metered_configured: false,
      anonymous_crawl_allowed: true,
      save_jobs_requires_auth: true,
    }
  }
  return res.json() as Promise<CrawlPricing>
}
