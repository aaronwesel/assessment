/**
 * Public landing — single hero, Trailmap brand, pricing + one primary CTA (no hero card clutter).
 */
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { fetchCrawlPricing, type CrawlPricing } from '../api/growth'
import { TrailmapLogo } from '../components/brand/TrailmapLogo'
import { config } from '../config'

const HOW_IT_WORKS = [
  {
    title: 'Paste a start URL',
    body: 'We queue your seed page on RabbitMQ and walk only internal HTML links — no guessing from AI.',
  },
  {
    title: 'Workers crawl & measure',
    body: 'Each parsed page gets Domain Link Ratio in .NET: internal links ÷ all links on that page.',
  },
  {
    title: 'Export the tree',
    body: 'Download CSV, JSON, or a readable report; retry with deeper depth or login assist when needed.',
  },
] as const

export function LandingPage() {
  const { isAuthenticated } = useAuth()
  const [pricing, setPricing] = useState<CrawlPricing | null>(null)

  useEffect(() => {
    void fetchCrawlPricing().then(setPricing)
  }, [])

  const perPage = pricing?.display_per_page ?? '$0.02'
  const freePages = pricing?.free_pages_allowance ?? 5000
  const examplePages = 24
  const exampleCost = pricing ? formatExampleCost(pricing, examplePages) : 'about half a dollar'
  const apiDocs = `${config.apiBaseUrl.replace(/\/$/, '')}/docs`

  return (
    <main className="landing landing-trailmap">
      <p className="landing-nav-row">
        <Link to="/crawl/history" className="landing-nav-link">
          History
        </Link>
        {isAuthenticated ? null : (
          <Link to="/login" className="landing-nav-link">
            Sign in
          </Link>
        )}
      </p>

      <section className="landing-hero-solo" aria-labelledby="landing-headline">
        <div className="landing-aurora" aria-hidden="true">
          <div className="landing-aurora-band" />
          <div className="landing-aurora-mesh" />
          <MotionNodes />
        </div>

        <div className="landing-hero-inner">
          <TrailmapLogo className="landing-wordmark" />

          <h1 id="landing-headline" className="landing-headline">
            Map every internal link on a site — with live progress and plain-language results.
          </h1>

          <p className="landing-tagline-wide">
            Trailmap Crawl is the assessment job crawler: queue a URL, watch the page tree grow, and understand{' '}
            <strong>Domain Link Ratio</strong> without reading a spec sheet.
          </p>

          <p className="landing-pricing-callout">
            <span className="landing-price-chip">
              {freePages.toLocaleString()} pages with results free
            </span>
            <span className="landing-price-chip landing-price-chip-secondary">
              then {perPage} per page (Stripe test)
            </span>
            <span className="hint">
              We bill only pages the worker parses successfully (link results). Example: ~{examplePages} pages ≈{' '}
              {exampleCost} after your free allowance.
            </span>
          </p>

          <div className="landing-cta-solo">
            <Link className="btn btn-primary landing-cta-main" to="/crawl/welcome">
              Start guided crawl
            </Link>
            {!isAuthenticated ? (
              <Link className="btn btn-ghost" to="/register">
                Save jobs to your account
              </Link>
            ) : (
              <Link className="btn btn-ghost" to="/crawl/history">
                View my history
              </Link>
            )}
          </div>

          <ol className="landing-steps-inline" aria-label="How it works in three steps">
            {HOW_IT_WORKS.map((item, i) => (
              <li key={item.title} className="landing-step-inline crawl-rise" style={{ animationDelay: `${0.12 * i}s` }}>
                <span className="landing-step-num">{i + 1}</span>
                <div>
                  <strong>{item.title}</strong>
                  <p>{item.body}</p>
                </div>
              </li>
            ))}
          </ol>
        </div>
      </section>

      <p className="landing-footnote hint">
        Quick path for experts: <Link to="/crawl">Start crawl</Link> · Platform API{' '}
        <a href={apiDocs} target="_blank" rel="noreferrer">
          interactive docs
        </a>{' '}
        · AI advises only; link parsing stays in .NET.
      </p>
    </main>
  )
}

function MotionNodes() {
  return (
    <>
      <span className="landing-node landing-node-a" />
      <span className="landing-node landing-node-b" />
      <span className="landing-node landing-node-c" />
    </>
  )
}

function formatExampleCost(pricing: CrawlPricing, pages: number): string {
  const cents = pricing.price_per_page_cents * pages
  return cents < 100 ? `${cents}¢` : `$${(cents / 100).toFixed(2)}`
}
