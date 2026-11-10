import { useEffect, useState, type FormEvent } from 'react'
import { crawlApi, CrawlApiError, type CrawlJob } from '../../api/crawl'

type Props = {
  jobId: string
  initialEmail: string | null
  statusAlertSent: boolean
  terminal: boolean
  onSaved?: (job: CrawlJob) => void
}

export function JobAlertPreference({
  jobId,
  initialEmail,
  statusAlertSent,
  terminal,
  onSaved,
}: Props) {
  const [email, setEmail] = useState(initialEmail ?? '')
  const [enabled, setEnabled] = useState(!!initialEmail)
  const [savedEmail, setSavedEmail] = useState(initialEmail)
  const [pending, setPending] = useState(false)
  const [note, setNote] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setEmail(initialEmail ?? '')
    setSavedEmail(initialEmail)
    setEnabled(!!initialEmail)
  }, [initialEmail])

  async function save(event: FormEvent) {
    event.preventDefault()
    setPending(true)
    setError(null)
    setNote(null)
    try {
      const nextEmail = enabled ? email.trim() : null
      const job = await crawlApi.setJobAlertEmail(jobId, nextEmail)
      onSaved?.(job)
      setSavedEmail(job.alertEmail)
      setNote(
        nextEmail
          ? terminal && job.statusAlertSent
            ? 'Alert email saved. We notified you when this job finished.'
            : 'Alert email saved. You will hear from us when the job completes or hits an issue (if SES is enabled on the server).'
          : 'Email alerts turned off for this job.',
      )
    } catch (err) {
      setError(err instanceof CrawlApiError ? err.message : 'Could not save alert preference.')
    } finally {
      setPending(false)
    }
  }

  const dirty = (enabled ? email.trim() : null) !== (savedEmail?.trim() || null) || enabled !== !!savedEmail

  return (
    <form
      className="crawl-alert-preference"
      onSubmit={(e) => void save(e)}
      aria-labelledby="job-alert-pref-heading"
    >
      <div className="crawl-alert-preference-head">
        <h3 id="job-alert-pref-heading" className="crawl-alert-preference-title">
          Email me when this finishes
        </h3>
        <label className="crawl-alert-toggle">
          <input
            type="checkbox"
            checked={enabled}
            onChange={(e) => setEnabled(e.target.checked)}
          />
          <span>Send alert</span>
        </label>
      </div>
      <p className="crawl-alert-preference-lede">
        Send me an alert when the job is complete or runs into issues.
      </p>
      {enabled ? (
        <label className="crawl-alert-email-field">
          <span className="sr-only">Email address</span>
          <input
            type="email"
            name="alertEmail"
            autoComplete="email"
            placeholder="you@example.com"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
        </label>
      ) : null}
      <div className="crawl-alert-preference-actions">
        <button type="submit" className="btn btn-ghost btn-sm" disabled={pending || (enabled && !email.trim()) || !dirty}>
          {pending ? 'Saving…' : 'Save preference'}
        </button>
        {statusAlertSent ? (
          <span className="hint">Completion alert queued or sent.</span>
        ) : null}
      </div>
      {note ? <p className="hint crawl-alert-note">{note}</p> : null}
      {error ? (
        <p className="form-error" role="alert">
          {error}
        </p>
      ) : null}
    </form>
  )
}
