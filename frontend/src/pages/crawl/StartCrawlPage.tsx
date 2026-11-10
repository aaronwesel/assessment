/**
 * Start Crawl — polished URL + maxDepth + maxPages form → Job Details.
 */
import { useEffect, useId, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { crawlApi, CrawlApiError } from '../../api/crawl'
import { CrawlShell } from '../../components/crawl/CrawlShell'
import {
  ABSOLUTE_MAX_PAGES,
  DEFAULT_MAX_PAGES,
  parseMaxPagesInput,
} from '../../components/crawl/crawlPageLimits'

type FieldErrors = { url?: string[]; maxDepth?: string[]; maxPages?: string[] }

function coerceUrl(input: string): string {
  const trimmed = input.trim()
  if (!trimmed) return ''
  if (/^https?:\/\//i.test(trimmed)) return trimmed
  if (trimmed.includes('.') && !trimmed.includes(' ')) return `https://${trimmed}`
  return trimmed
}

function validate(url: string, maxDepth: string, maxPages: string): FieldErrors {
  const errors: FieldErrors = {}
  const normalized = coerceUrl(url)
  try {
    const parsed = new URL(normalized)
    if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') {
      errors.url = ['URL must use http or https.']
    }
  } catch {
    errors.url = ['Enter a site like example.com or https://example.com/']
  }
  if (maxDepth !== '' && !/^[0-5]$/.test(maxDepth)) {
    errors.maxDepth = ['Max depth must be a whole number between 0 and 5.']
  }
  if (maxPages !== '') {
    const parsed = parseMaxPagesInput(maxPages)
    if (parsed === undefined) {
      errors.maxPages = [`Max pages must be a whole number between 1 and ${ABSOLUTE_MAX_PAGES}.`]
    }
  }
  return errors
}

function serverFieldErrors(error: unknown): FieldErrors | undefined {
  if (!(error instanceof CrawlApiError) || !error.problem.errors) return undefined
  const { Url, MaxDepth, MaxPages, url, maxDepth, maxPages } = error.problem.errors
  return {
    url: Url ?? url,
    maxDepth: MaxDepth ?? maxDepth,
    maxPages: MaxPages ?? maxPages,
  }
}

export function StartCrawlPage() {
  const navigate = useNavigate()
  const urlRef = useRef<HTMLInputElement>(null)
  const formId = useId()
  const [url, setUrl] = useState('')
  const [maxDepth, setMaxDepth] = useState('')
  const [maxPages, setMaxPages] = useState(String(DEFAULT_MAX_PAGES))
  const [clientErrors, setClientErrors] = useState<FieldErrors>({})
  const [submitError, setSubmitError] = useState<unknown>(null)
  const [pending, setPending] = useState(false)

  const serverErrors = serverFieldErrors(submitError)
  const urlErrors = clientErrors.url ?? serverErrors?.url
  const maxDepthErrors = clientErrors.maxDepth ?? serverErrors?.maxDepth
  const maxPagesErrors = clientErrors.maxPages ?? serverErrors?.maxPages

  useEffect(() => {
    urlRef.current?.focus()
  }, [])

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const trimmedUrl = coerceUrl(url)
    const errors = validate(trimmedUrl, maxDepth, maxPages)
    setClientErrors(errors)
    setSubmitError(null)
    if (errors.url || errors.maxDepth || errors.maxPages) {
      urlRef.current?.focus()
      return
    }

    setPending(true)
    try {
      const { jobId } = await crawlApi.createJob({
        url: trimmedUrl,
        maxDepth: maxDepth === '' ? undefined : Number(maxDepth),
        maxPages: parseMaxPagesInput(maxPages),
      })
      navigate(`/crawl/jobs/${jobId}`)
    } catch (err) {
      setSubmitError(err)
    } finally {
      setPending(false)
    }
  }

  const onDepthKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Escape') {
      setMaxDepth('')
    }
  }

  return (
    <CrawlShell
      title="Start a crawl"
      lede="Point the worker at a start URL. Choose depth and page budget before queueing — both are sent to the crawl API."
      actions={
        <>
          <Link className="btn btn-ghost" to="/crawl/welcome">
            Welcome wizard
          </Link>
          <Link className="btn btn-ghost" to="/crawl/history">
            View history
          </Link>
        </>
      }
    >
      <section className="crawl-start-panel crawl-rise-delay" aria-labelledby={`${formId}-heading`}>
        <h2 id={`${formId}-heading`} className="visually-hidden">
          Crawl settings
        </h2>
        <form
          className="crawl-start-form"
          onSubmit={(e) => void handleSubmit(e)}
          noValidate
        >
          <label className="field crawl-field" htmlFor="crawl-url">
            <span>Start URL</span>
            <input
              ref={urlRef}
              id="crawl-url"
              name="url"
              type="url"
              inputMode="url"
              value={url}
              onChange={(event) => setUrl(event.target.value)}
              placeholder="https://www.scrapethissite.com/pages/"
              required
              autoComplete="url"
              spellCheck={false}
              aria-invalid={!!urlErrors}
              aria-describedby={urlErrors ? 'crawl-url-error' : 'crawl-url-hint'}
            />
          </label>
          <p id="crawl-url-hint" className="hint">
            Absolute http(s) address. HTML pages only — the worker ignores non-HTML.
          </p>
          {urlErrors ? (
            <p id="crawl-url-error" className="crawl-soft-note">
              {urlErrors.join(' ')} — try example.com and we will add https:// for you.
            </p>
          ) : null}

          <div className="crawl-start-row crawl-scope-row">
            <label className="field crawl-field crawl-field-narrow" htmlFor="crawl-max-depth">
              <span>Max depth</span>
              <input
                id="crawl-max-depth"
                name="maxDepth"
                type="number"
                min={0}
                max={5}
                step={1}
                value={maxDepth}
                onChange={(event) => setMaxDepth(event.target.value)}
                onKeyDown={onDepthKeyDown}
                placeholder="2"
                aria-invalid={!!maxDepthErrors}
                aria-describedby={
                  maxDepthErrors
                    ? 'crawl-max-depth-hint crawl-max-depth-error'
                    : 'crawl-max-depth-hint'
                }
              />
            </label>
            <label className="field crawl-field crawl-field-narrow" htmlFor="crawl-max-pages">
              <span>Max pages to crawl</span>
              <input
                id="crawl-max-pages"
                name="maxPages"
                type="number"
                min={1}
                max={ABSOLUTE_MAX_PAGES}
                step={1}
                value={maxPages}
                onChange={(event) => setMaxPages(event.target.value)}
                aria-invalid={!!maxPagesErrors}
                aria-describedby={
                  maxPagesErrors ? 'crawl-max-pages-hint crawl-max-pages-error' : 'crawl-max-pages-hint'
                }
              />
            </label>
          </div>
          <p id="crawl-max-depth-hint" className="hint">
            Depth 0–5 · empty depth uses server default (2). Esc clears depth.
          </p>
          <p id="crawl-max-pages-hint" className="hint">
            Page budget 1–{ABSOLUTE_MAX_PAGES} (default {DEFAULT_MAX_PAGES}). Hard stop when the queue hits this cap.
          </p>
          {maxDepthErrors ? (
            <p id="crawl-max-depth-error" className="form-error">
              {maxDepthErrors.join(' ')}
            </p>
          ) : null}
          {maxPagesErrors ? (
            <p id="crawl-max-pages-error" className="form-error">
              {maxPagesErrors.join(' ')}
            </p>
          ) : null}

          <div className="crawl-start-cta">
            <button type="submit" className="btn btn-primary crawl-cta" disabled={pending}>
              {pending ? 'Starting crawl…' : 'Start crawl'}
            </button>
            <p className="hint">
              Next: live job details with progress, tree, and Domain Link Ratio.
            </p>
          </div>
        </form>

        {submitError && !serverErrors ? (
          <p className="form-error crawl-submit-error" role="alert">
            {submitError instanceof CrawlApiError
              ? submitError.message
              : 'Could not start crawl.'}
          </p>
        ) : null}
      </section>

      <aside className="crawl-start-aside crawl-rise-delay-2" aria-label="What happens next">
        <ol className="crawl-steps">
          <li>
            <strong>Queue</strong>
            <span>Job is accepted with your page budget and the root page is enqueued.</span>
          </li>
          <li>
            <strong>Crawl</strong>
            <span>Workers fetch HTML, normalize URLs, and follow internal links.</span>
          </li>
          <li>
            <strong>Measure</strong>
            <span>Each page gets a Domain Link Ratio when parsing finishes.</span>
          </li>
        </ol>
      </aside>
    </CrawlShell>
  )
}
