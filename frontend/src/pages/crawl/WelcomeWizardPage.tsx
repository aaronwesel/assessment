/**
 * AI-guided welcome wizard — friendly URL coaching, not a harsh absolute-URL wall.
 * Final step submits to Crawl.Api POST /api/jobs (anonymous OK).
 */
import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { crawlApi, CrawlApiError } from '../../api/crawl'
import { fetchCrawlPricing, type CrawlPricing } from '../../api/growth'
import { CrawlShell } from '../../components/crawl/CrawlShell'

const STEPS = ['Welcome', 'Your site', 'Depth & limits', 'Review'] as const

function coerceUrl(input: string): string {
  const trimmed = input.trim()
  if (!trimmed) return ''
  if (/^https?:\/\//i.test(trimmed)) return trimmed
  if (trimmed.includes('.') && !trimmed.includes(' ')) return `https://${trimmed}`
  return trimmed
}

export function WelcomeWizardPage() {
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const { isAuthenticated } = useAuth()
  const [step, setStep] = useState(0)
  const [urlInput, setUrlInput] = useState(params.get('url') ?? '')
  const [depth, setDepth] = useState(params.get('depth') ?? '2')
  const retryMode = params.get('retry') ?? ''
  const [pricing, setPricing] = useState<CrawlPricing | null>(null)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const normalized = useMemo(() => coerceUrl(urlInput), [urlInput])
  const urlLooksValid = useMemo(() => {
    try {
      const p = new URL(normalized)
      return p.protocol === 'http:' || p.protocol === 'https:'
    } catch {
      return false
    }
  }, [normalized])

  useEffect(() => {
    void fetchCrawlPricing().then(setPricing)
  }, [])

  useEffect(() => {
    if (retryMode === 'login') setStep(1)
  }, [retryMode])

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!urlLooksValid) {
      setError('We need a reachable https address — example: https://www.scrapethissite.com/pages/')
      setStep(1)
      return
    }
    setPending(true)
    setError(null)
    try {
      const { jobId } = await crawlApi.createJob({
        url: normalized,
        maxDepth: depth === '' ? undefined : Number(depth),
      })
      navigate(`/crawl/jobs/${jobId}`)
    } catch (err) {
      setError(err instanceof CrawlApiError ? err.message : 'Could not start crawl.')
    } finally {
      setPending(false)
    }
  }

  return (
    <CrawlShell
      title="Welcome wizard"
      lede="Friendly setup for your first crawl — fix URLs in plain language, see per-page pricing, then launch."
      actions={
        <Link className="btn btn-ghost" to="/crawl">
          Skip to quick start
        </Link>
      }
    >
      <nav className="crawl-wizard-steps" aria-label="Wizard progress">
        {STEPS.map((label, i) => (
          <span
            key={label}
            className={`crawl-wizard-step${i === step ? ' is-active' : ''}${i < step ? ' is-done' : ''}`}
          >
            {i + 1}. {label}
          </span>
        ))}
      </nav>

      <form className="crawl-wizard-panel" onSubmit={(e) => void submit(e)}>
        {step === 0 ? (
          <section>
            <h2>What this tool does</h2>
            <p>
              Assessment Crawl walks <strong>internal HTML links</strong> from your start URL, computes{' '}
              <strong>Domain Link Ratio</strong> per page, and gives you exports plus AI assist for empty
              results and login walls.
            </p>
            <ul className="crawl-wizard-bullets">
              <li>Anonymous crawls work locally — no account required.</li>
              <li>
                {pricing
                  ? `Test pricing: ${pricing.display_per_page} per page crawled (metered Stripe when configured).`
                  : 'Test pricing shown on the landing page.'}
              </li>
              <li>
                {isAuthenticated
                  ? 'Your jobs are tied to your session — view History anytime.'
                  : (
                    <>
                      <Link to="/register">Register</Link> to save jobs and enable billing test mode.
                    </>
                  )}
              </li>
            </ul>
            {retryMode === 'login' ? (
              <p className="crawl-wizard-callout">
                Retry mode: login assist — after the crawl, use Job Details → Assisted login with an HTML
                snapshot from the sign-in page.
              </p>
            ) : null}
          </section>
        ) : null}

        {step === 1 ? (
          <section>
            <h2>Your start URL</h2>
            <p className="hint">
              Type a domain or full link — we add https:// when missing. Use a folder URL if the homepage
              is sparse.
            </p>
            <label className="field crawl-field">
              <span>Website</span>
              <input
                value={urlInput}
                onChange={(e) => setUrlInput(e.target.value)}
                placeholder="scrapethissite.com/pages/"
                autoFocus
              />
            </label>
            {urlInput && !urlLooksValid ? (
              <p className="form-error">Try including a dot in the host, e.g. example.com/docs</p>
            ) : null}
            {urlLooksValid ? (
              <p className="hint">
                Will crawl: <code>{normalized}</code>
              </p>
            ) : null}
          </section>
        ) : null}

        {step === 2 ? (
          <section>
            <h2>Depth & limits</h2>
            <label className="field crawl-field crawl-field-narrow">
              <span>Max depth (0–5)</span>
              <input
                type="number"
                min={0}
                max={5}
                value={depth}
                onChange={(e) => setDepth(e.target.value)}
              />
            </label>
            <p className="hint">
              Depth 2 is the default — good for demo sites. Increase when retrying a shallow crawl.
            </p>
          </section>
        ) : null}

        {step === 3 ? (
          <section>
            <h2>Review</h2>
            <dl className="crawl-fact-grid">
              <div>
                <dt>URL</dt>
                <dd>{normalized || '—'}</dd>
              </div>
              <div>
                <dt>Max depth</dt>
                <dd>{depth || '2 (server default)'}</dd>
              </div>
              <div>
                <dt>Estimated cost (test)</dt>
                <dd>{pricing ? `${pricing.display_per_page} / page` : '—'}</dd>
              </div>
            </dl>
            <p className="hint">Submit creates a .NET job and enqueues RabbitMQ work immediately.</p>
          </section>
        ) : null}

        {error ? (
          <p className="form-error" role="alert">
            {error}
          </p>
        ) : null}

        <div className="crawl-wizard-nav">
          {step > 0 ? (
            <button type="button" className="btn btn-ghost" onClick={() => setStep((s) => s - 1)}>
              Back
            </button>
          ) : (
            <span />
          )}
          {step < STEPS.length - 1 ? (
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => setStep((s) => s + 1)}
              disabled={step === 1 && !urlLooksValid}
            >
              Continue
            </button>
          ) : (
            <button type="submit" className="btn btn-primary crawl-cta" disabled={pending || !urlLooksValid}>
              {pending ? 'Starting…' : 'Start crawl'}
            </button>
          )}
        </div>
      </form>
    </CrawlShell>
  )
}
