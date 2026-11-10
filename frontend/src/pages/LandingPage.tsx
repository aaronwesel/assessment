/**
 * Public landing — single hero, Trailmap brand, pricing + one primary CTA (no hero card clutter).
 */
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { fetchCrawlPricing, type CrawlPricing } from '../api/growth'
import { TrailmapLogo } from '../components/brand/TrailmapLogo'

const HOW_IT_WORKS = [
  {
    title: 'What is a Web Crawler?',
    body: (
      <>
        An automated explorer for your site. Give us a start URL, we follow internal links, and map how pages
        connect — including simple health metrics along the way.
      </>
    ),
  },
  {
    title: 'Try it — generous free tier',
    body: (
      <>
        Free for the first 5,000 pages. Save jobs to your account, then export from the UI or plug into our{' '}
        <Link to="/swagger">interactive Crawl API docs</Link> (and <Link to="/docs">Platform API</Link> when you need
        billing or auth).
      </>
    ),
  },
  {
    title: 'Guided setup & alerts',
    body: 'A friendly AI coach walks you through each step of setup. On the live progress screen you can optionally add an email for completion or issue alerts — so you don’t have to watch the job the whole time.',
  },
] as const

export function LandingPage() {
  const { isAuthenticated } = useAuth()
  const [pricing, setPricing] = useState<CrawlPricing | null>(null)

  useEffect(() => {
    void fetchCrawlPricing().then(setPricing)
  }, [])

  const perPage = pricing?.display_per_page ?? '$0.02'
  const examplePages = 24
  const exampleCost = pricing ? formatExampleCost(pricing, examplePages) : 'about half a dollar'

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
            Trailmap Crawl helps you see how a website fits together — start with one URL, watch the map grow, and
            export what you find without learning crawler jargon first.
          </p>

          <p className="landing-pricing-callout">
            <span className="landing-price-chip">{perPage} per page crawled</span>
            <span className="hint">
              Example: a depth‑2 demo on books.toscrape.com (~{examplePages} pages) ≈ {exampleCost} in Stripe test
              mode.
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

          <ol className="landing-steps-inline" aria-label="How Trailmap Crawl works">
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
        Prefer fewer steps? <Link to="/crawl">Quick start</Link> skips the wizard · API explorers:{' '}
        <Link to="/swagger">Crawl</Link> · <Link to="/docs">Platform</Link>
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
