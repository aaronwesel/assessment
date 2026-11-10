/**
 * Start Crawl — URL + optional maxDepth → navigate to Job Details.
 */
import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { crawlApi, CrawlApiError } from '../../api/crawl'

type FieldErrors = { url?: string[]; maxDepth?: string[] }

function validate(url: string, maxDepth: string): FieldErrors {
  const errors: FieldErrors = {}
  try {
    const parsed = new URL(url)
    if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') {
      errors.url = ['URL must use http or https.']
    }
  } catch {
    errors.url = ['Enter an absolute URL, e.g. https://example.com.']
  }
  if (maxDepth !== '' && !/^[0-5]$/.test(maxDepth)) {
    errors.maxDepth = ['Max depth must be a whole number between 0 and 5.']
  }
  return errors
}

function serverFieldErrors(error: unknown): FieldErrors | undefined {
  if (!(error instanceof CrawlApiError) || !error.problem.errors) return undefined
  const { Url, MaxDepth, url, maxDepth } = error.problem.errors
  return { url: Url ?? url, maxDepth: MaxDepth ?? maxDepth }
}

export function StartCrawlPage() {
  const navigate = useNavigate()
  const [url, setUrl] = useState('')
  const [maxDepth, setMaxDepth] = useState('')
  const [clientErrors, setClientErrors] = useState<FieldErrors>({})
  const [submitError, setSubmitError] = useState<unknown>(null)
  const [pending, setPending] = useState(false)

  const serverErrors = serverFieldErrors(submitError)
  const urlErrors = clientErrors.url ?? serverErrors?.url
  const maxDepthErrors = clientErrors.maxDepth ?? serverErrors?.maxDepth

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const trimmedUrl = url.trim()
    const errors = validate(trimmedUrl, maxDepth)
    setClientErrors(errors)
    setSubmitError(null)
    if (errors.url || errors.maxDepth) return

    setPending(true)
    try {
      const { jobId } = await crawlApi.createJob({
        url: trimmedUrl,
        maxDepth: maxDepth === '' ? undefined : Number(maxDepth),
      })
      navigate(`/crawl/jobs/${jobId}`)
    } catch (err) {
      setSubmitError(err)
    } finally {
      setPending(false)
    }
  }

  return (
    <main className="page crawl-page">
      <div className="crawl-header">
        <h1>Start crawl</h1>
        <Link className="nav-link" to="/crawl/history">
          History
        </Link>
      </div>
      <p className="lede">
        Submit a start URL. The worker walks internal links and reports Domain Link
        Ratio when the job completes.
      </p>
      <form className="password-form" onSubmit={(e) => void handleSubmit(e)} noValidate>
        <label className="field" htmlFor="crawl-url">
          <span>Start URL</span>
          <input
            id="crawl-url"
            type="url"
            value={url}
            onChange={(event) => setUrl(event.target.value)}
            placeholder="https://www.scrapethissite.com/pages/"
            required
            autoComplete="url"
            aria-invalid={!!urlErrors}
            aria-describedby={urlErrors ? 'crawl-url-error' : undefined}
          />
        </label>
        {urlErrors ? (
          <p id="crawl-url-error" className="form-error">
            {urlErrors.join(' ')}
          </p>
        ) : null}

        <label className="field" htmlFor="crawl-max-depth">
          <span>Max depth (0–5, optional)</span>
          <input
            id="crawl-max-depth"
            type="number"
            min={0}
            max={5}
            step={1}
            value={maxDepth}
            onChange={(event) => setMaxDepth(event.target.value)}
            placeholder="2"
            aria-invalid={!!maxDepthErrors}
            aria-describedby={
              maxDepthErrors
                ? 'crawl-max-depth-hint crawl-max-depth-error'
                : 'crawl-max-depth-hint'
            }
          />
        </label>
        <p id="crawl-max-depth-hint" className="hint">
          Leave empty to use the server default (2).
        </p>
        {maxDepthErrors ? (
          <p id="crawl-max-depth-error" className="form-error">
            {maxDepthErrors.join(' ')}
          </p>
        ) : null}

        <button type="submit" className="btn btn-primary" disabled={pending}>
          {pending ? 'Starting…' : 'Start crawl'}
        </button>
      </form>
      {submitError && !serverErrors ? (
        <p className="form-error" role="alert">
          {submitError instanceof CrawlApiError
            ? submitError.message
            : 'Could not start crawl.'}
        </p>
      ) : null}
    </main>
  )
}
