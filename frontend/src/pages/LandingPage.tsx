/**
 * Public marketing landing — crawler product hero, per-page pricing, wizard CTA.
 */
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { fetchCrawlPricing, type CrawlPricing } from '../api/growth'

export function LandingPage() {
  const { isAuthenticated } = useAuth()
  const [pricing, setPricing] = useState<CrawlPricing | null>(null)

  useEffect(() => {
    void fetchCrawlPricing().then(setPricing)
  }, [])

  return (
    <main className="landing landing-crawl">
      <section className="landing-hero" aria-labelledby="landing-brand">
        <div className="landing-atmosphere" aria-hidden="true">
          <div className="landing-orb landing-orb-a" />
          <div className="landing-orb landing-orb-b" />
          <div className="landing-grid" />
        </div>

        <div className="landing-hero-copy">
          <p className="landing-kicker">
            <img src="/favicon.svg" alt="" className="landing-logo" width={28} height={28} />
            Job-based web crawler
          </p>
          <h1 id="landing-brand" className="landing-brand">
            Assessment Crawl
          </h1>
          <p className="landing-tagline">
            Start a URL, walk internal links with RabbitMQ workers, and read Domain Link Ratio on every
            page — with AI help when results are empty or behind login.
          </p>

          {pricing ? (
            <p className="landing-price">
              <strong>{pricing.display_per_page}</strong> per page (Stripe test meter){' '}
              <span className="hint">· anonymous crawls OK locally</span>
            </p>
          ) : null}

          <div className="landing-cta">
            <Link className="btn btn-primary landing-btn" to="/crawl/welcome">
              Guided setup
            </Link>
            <Link className="btn btn-ghost landing-btn" to="/crawl">
              Start crawl
            </Link>
            {isAuthenticated ? (
              <Link className="btn btn-ghost landing-btn" to="/crawl/history">
                History
              </Link>
            ) : (
              <>
                <Link className="btn btn-ghost landing-btn" to="/register">
                  Register to save jobs
                </Link>
                <Link className="btn btn-ghost landing-btn" to="/login">
                  Sign in
                </Link>
              </>
            )}
          </div>
        </div>

        <div className="landing-visual" aria-hidden="true">
          <div className="landing-panel landing-panel-crawl">
            <span className="landing-panel-label">What you get</span>
            <ul className="landing-panel-list">
              <li>Live progress + page tree</li>
              <li>Domain Link Ratio meters</li>
              <li>CSV / JSON export</li>
              <li>Empty-result AI diagnosis</li>
              <li>Element pick (CSS / XPath)</li>
              <li>Login retry assist</li>
            </ul>
          </div>
        </div>
      </section>

      <section className="landing-strip" aria-label="How it works">
        <ol className="landing-how">
          <li>
            <strong>Queue</strong>
            <span>.NET API accepts the job and enqueues the root page on RabbitMQ.</span>
          </li>
          <li>
            <strong>Crawl</strong>
            <span>Workers fetch HTML, normalize URLs, and follow internal links deterministically.</span>
          </li>
          <li>
            <strong>Results</strong>
            <span>Job Details shows DLR, exports, and AI-guided retry when pages are missing.</span>
          </li>
        </ol>
      </section>
    </main>
  )
}
