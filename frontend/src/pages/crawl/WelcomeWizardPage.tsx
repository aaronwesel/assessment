/**
 * Six-step guided crawl (steps 1–4 here; 5–6 on Job Details). Friendly URL coaching + DeepSeek guide.
 */
import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { crawlApi, CrawlApiError } from '../../api/crawl'
import { fetchCrawlPricing, type CrawlPricing } from '../../api/growth'
import { AdvancedOptionsDrawer } from '../../components/crawl/AdvancedOptionsDrawer'
import { CrawlFlowSteps } from '../../components/crawl/CrawlFlowSteps'
import { CrawlShell } from '../../components/crawl/CrawlShell'
import { DeepSeekGuide } from '../../components/crawl/DeepSeekGuide'
import {
  DEFAULT_ADVANCED,
  loadAdvancedFromStorage,
  parseAdvancedFromSearch,
  saveAdvancedToStorage,
  type CrawlAdvancedOptions,
} from '../../components/crawl/crawlAdvancedOptions'
import {
  ABSOLUTE_MAX_PAGES,
  DEFAULT_MAX_PAGES,
  clampMaxPages,
} from '../../components/crawl/crawlPageLimits'

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
  const [advanced, setAdvanced] = useState<CrawlAdvancedOptions>(() => ({
    ...DEFAULT_ADVANCED,
    ...loadAdvancedFromStorage(),
    ...parseAdvancedFromSearch(params),
  }))
  const [drawerOpen, setDrawerOpen] = useState(params.get('advanced') === '1')
  const [pricing, setPricing] = useState<CrawlPricing | null>(null)
  const [pending, setPending] = useState(false)
  const [softNote, setSoftNote] = useState<string | null>(null)

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
    if (params.get('retry') === 'login') setStep(0)
    const stepParam = params.get('step')
    if (stepParam != null && stepParam !== '') {
      const n = Number(stepParam)
      if (!Number.isNaN(n)) setStep(Math.min(3, Math.max(0, n)))
    }
    if (params.get('advanced') === '1') setDrawerOpen(true)
    saveAdvancedToStorage(advanced)
  }, [params, advanced])

  const goNext = () => {
    if (step === 0 && urlInput.trim() && !urlLooksValid) {
      const coerced = coerceUrl(urlInput)
      if (coerced !== urlInput) {
        setUrlInput(coerced)
        setSoftNote('We added https:// for you — click Continue when the address looks right.')
        return
      }
      setSoftNote('Try a host with a dot, like example.com or https://books.toscrape.com')
      return
    }
    setSoftNote(null)
    setStep((s) => Math.min(s + 1, 3))
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!urlLooksValid) {
      setSoftNote('We need a reachable https address — we can fix missing https:// automatically.')
      setStep(0)
      return
    }
    setPending(true)
    setSoftNote(null)
    try {
      const { jobId } = await crawlApi.createJob({
        url: normalized,
        maxDepth: advanced.maxDepth,
        maxPages: advanced.maxPages,
      })
      navigate(`/crawl/jobs/${jobId}?guided=1`)
    } catch (err) {
      setSoftNote(err instanceof CrawlApiError ? err.message : 'Could not start crawl — try again in a moment.')
    } finally {
      setPending(false)
    }
  }

  const estPages = Math.min(advanced.maxPages, Math.pow(3, advanced.maxDepth + 1))
  const estCost =
    pricing && estPages
      ? `up to ~${estPages} pages × ${pricing.display_per_page}`
      : pricing?.display_per_page ?? 'per page'

  return (
    <CrawlShell
      title="Guided crawl"
      lede="Six numbered steps — plain language at each stage. AI guides; .NET workers parse links."
      wide
      actions={
        <>
          <button type="button" className="btn btn-ghost" onClick={() => setDrawerOpen(true)}>
            Advanced options
          </button>
          <Link className="btn btn-ghost" to="/crawl">
            Quick start
          </Link>
        </>
      }
    >
      <div className="crawl-guided-layout">
        <div className="crawl-guided-main">
          <CrawlFlowSteps activeStep={step + 1} maxReachable={4} />

          <form className="crawl-wizard-panel crawl-wizard-trailmap" onSubmit={(e) => void submit(e)}>
            {step === 0 ? (
              <section className="crawl-wizard-section">
                <h2>Enter your website</h2>
                <p className="hint">
                  Paste a homepage or a deeper folder (e.g. /pages/). We add https:// when it is missing.
                </p>
                <label className="field crawl-field">
                  <span>Start URL</span>
                  <input
                    value={urlInput}
                    onChange={(e) => setUrlInput(e.target.value)}
                    placeholder="books.toscrape.com"
                    autoFocus
                    aria-invalid={urlInput.length > 0 && !urlLooksValid}
                  />
                </label>
                {softNote ? <p className="crawl-soft-note">{softNote}</p> : null}
                {urlLooksValid ? (
                  <p className="hint">
                    Will crawl <code>{normalized}</code>
                  </p>
                ) : null}
              </section>
            ) : null}

            {step === 1 ? (
              <section className="crawl-wizard-section">
                <h2>Review what we found</h2>
                <p className="hint">
                  The crawl guide (right) probes this URL from the server and explains reachability, login walls, and
                  depth tips — without parsing your link graph.
                </p>
                <dl className="crawl-fact-grid">
                  <div>
                    <dt>Normalized URL</dt>
                    <dd>{normalized}</dd>
                  </div>
                  <div>
                    <dt>Planned depth</dt>
                    <dd>{advanced.maxDepth}</dd>
                  </div>
                  <div>
                    <dt>Page budget</dt>
                    <dd>up to {advanced.maxPages} pages (max {ABSOLUTE_MAX_PAGES})</dd>
                  </div>
                </dl>
              </section>
            ) : null}

            {step === 2 ? (
              <section className="crawl-wizard-section">
                <h2>Choose what to collect</h2>
                <p className="hint">
                  Depth controls how far we follow internal links. Page budget caps queue size and test meter exposure
                  (1–{ABSOLUTE_MAX_PAGES}).
                </p>
                <div className="crawl-scope-row">
                  <label className="field crawl-field-narrow">
                    <span>Max depth</span>
                    <input
                      type="number"
                      min={0}
                      max={5}
                      value={advanced.maxDepth}
                      onChange={(e) =>
                        setAdvanced((a) => ({ ...a, maxDepth: Number(e.target.value) || 0 }))
                      }
                    />
                  </label>
                  <label className="field crawl-field-narrow">
                    <span>Max pages to crawl</span>
                    <input
                      type="number"
                      min={1}
                      max={ABSOLUTE_MAX_PAGES}
                      value={advanced.maxPages}
                      onChange={(e) =>
                        setAdvanced((a) => ({
                          ...a,
                          maxPages: clampMaxPages(Number(e.target.value)),
                        }))
                      }
                      aria-describedby="wizard-max-pages-hint"
                    />
                  </label>
                </div>
                <p id="wizard-max-pages-hint" className="hint">
                  Default {DEFAULT_MAX_PAGES}; hard cap {ABSOLUTE_MAX_PAGES}. Sent to the API when you start.
                </p>
                <p className="hint">
                  Selector mode <strong>{advanced.selectorMode.toUpperCase()}</strong>
                  {advanced.elementPickGoal
                    ? ` · field goal: “${advanced.elementPickGoal}”`
                    : ' — set a goal in Advanced options for AI field picking later.'}
                </p>
              </section>
            ) : null}

            {step === 3 ? (
              <section className="crawl-wizard-section">
                <h2>Confirm cost &amp; start</h2>
                <dl className="crawl-fact-grid">
                  <div>
                    <dt>URL</dt>
                    <dd>{normalized}</dd>
                  </div>
                  <div>
                    <dt>Limits</dt>
                    <dd>
                      depth {advanced.maxDepth} · max {advanced.maxPages} pages
                    </dd>
                  </div>
                  <div>
                    <dt>Estimated test cost</dt>
                    <dd>{estCost}</dd>
                  </div>
                </dl>
                <p className="hint">
                  {isAuthenticated
                    ? 'Job saves to your account and shows in History.'
                    : (
                      <>
                        Anonymous run OK locally — <Link to="/register">register</Link> to save jobs &amp; billing.
                      </>
                    )}
                </p>
                <p className="hint">After start you land on step 5 (live progress) then step 6 (export).</p>
              </section>
            ) : null}

            <div className="crawl-wizard-nav">
              {step > 0 ? (
                <button type="button" className="btn btn-ghost" onClick={() => setStep((s) => s - 1)}>
                  Back
                </button>
              ) : (
                <span />
              )}
              {step < 3 ? (
                <button type="button" className="btn btn-primary" onClick={goNext} disabled={step === 0 && !urlInput.trim()}>
                  Continue
                </button>
              ) : (
                <button type="submit" className="btn btn-primary crawl-cta" disabled={pending || !urlLooksValid}>
                  {pending ? 'Starting…' : 'Start crawl'}
                </button>
              )}
            </div>
          </form>
        </div>

        <DeepSeekGuide step={step} url={normalized} maxDepth={advanced.maxDepth} />
      </div>

      <AdvancedOptionsDrawer
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        value={advanced}
        onChange={setAdvanced}
      />
    </CrawlShell>
  )
}
