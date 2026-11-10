/**
 * Persistent setup coach — server probe via /api/ai/setup/preview; degrades without DeepSeek key.
 */
import { useCallback, useEffect, useState, type FormEvent } from 'react'
import {
  fetchAiFeatures,
  fetchSetupPreview,
  type SetupPreviewResponse,
} from '../../crawl/ai/crawlAiApi'
import {
  AI_OFFLINE_SETUP_STEPS,
  answerSourceLabel,
  assistiveAiHeadline,
  formatDegradedReason,
} from '../../crawl/ai/aiStatus'
import { STEP_LABELS } from './CrawlFlowSteps'

type Props = {
  step: number
  url: string
  maxDepth: number
  collapsed?: boolean
}

export function DeepSeekGuide({ step, url, maxDepth, collapsed }: Props) {
  const [preview, setPreview] = useState<SetupPreviewResponse | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [question, setQuestion] = useState('')
  const [answer, setAnswer] = useState<string | null>(null)
  const [answerUsedModel, setAnswerUsedModel] = useState(false)
  const [panelOpen, setPanelOpen] = useState(!collapsed)
  const [aiOnline, setAiOnline] = useState<boolean | null>(null)

  useEffect(() => {
    let cancelled = false
    fetchAiFeatures()
      .then((f) => {
        if (!cancelled) setAiOnline(f.deepSeekConfigured)
      })
      .catch(() => {
        if (!cancelled) setAiOnline(false)
      })
    return () => {
      cancelled = true
    }
  }, [])

  const refresh = useCallback(
    async (q?: string) => {
      if (!url.trim()) {
        setPreview(null)
        return
      }
      setLoading(true)
      setError(null)
      try {
        const resp = await fetchSetupPreview({
          url,
          maxDepth,
          wizardStep: step,
          question: q,
        })
        setPreview(resp)
        if (q && resp.modelAnswer) {
          setAnswer(resp.modelAnswer)
          setAnswerUsedModel(resp.usedModel)
        }
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Guide unavailable')
      } finally {
        setLoading(false)
      }
    },
    [url, maxDepth, step],
  )

  useEffect(() => {
    if (step === 1 && url.trim()) void refresh()
  }, [step, url, maxDepth, refresh])

  const onAsk = (event: FormEvent) => {
    event.preventDefault()
    const q = question.trim()
    if (!q) return
    void refresh(q)
  }

  const stepTitle = STEP_LABELS[step] ?? 'Guide'
  const online = aiOnline === true
  const offline = aiOnline === false
  const degradedHint = formatDegradedReason(preview?.degradedReason)

  return (
    <aside
      className={`crawl-guide${panelOpen ? ' is-open' : ' is-collapsed'}${offline ? ' is-ai-offline' : ''}`}
      aria-label="DeepSeek crawl guide"
    >
      <header className="crawl-guide-head">
        <MotionDot online={online} />
        <div>
          <h2 className="crawl-guide-title">Crawl guide</h2>
          <p className="hint crawl-guide-step">
            Step {step + 1}: {stepTitle}
          </p>
          {aiOnline != null ? (
            <p className={`crawl-guide-ai-status${online ? ' is-online' : ' is-offline'}`} role="status">
              {assistiveAiHeadline(online)}
            </p>
          ) : null}
        </div>
        <button
          type="button"
          className="btn btn-ghost crawl-guide-toggle"
          onClick={() => setPanelOpen((o) => !o)}
          aria-expanded={panelOpen}
        >
          {panelOpen ? 'Hide' : 'Show'}
        </button>
      </header>

      {panelOpen ? (
        <div className="crawl-guide-body">
          {offline ? (
            <div className="crawl-guide-offline" role="note">
              <p>
                AI coaching is in <strong>offline</strong> mode — server probes and labeled heuristics still work. Link
                parsing and Domain Link Ratio stay in .NET either way.
              </p>
              <p className="hint">Turn AI on:</p>
              <ol className="crawl-guide-setup-steps">
                {AI_OFFLINE_SETUP_STEPS.map((line) => (
                  <li key={line}>{line}</li>
                ))}
              </ol>
            </div>
          ) : null}

          <p className="crawl-guide-lede">{preview?.stepAdvice ?? friendlyStepCopy(step)}</p>

          {loading ? <p className="hint">Checking your site…</p> : null}
          {error ? (
            <p className="crawl-guide-soft-warn" role="status">
              {error}. You can still continue — link parsing stays in .NET either way.
            </p>
          ) : null}

          {preview ? (
            <ul className="crawl-guide-obs">
              {preview.observations.map((line) => (
                <li key={line}>{line}</li>
              ))}
              {preview.likelyLoginWall ? (
                <li className="crawl-guide-flag">Login wall likely — plan for login assist after the crawl.</li>
              ) : null}
            </ul>
          ) : null}

          {answer ? (
            <div className="crawl-guide-answer">
              <span className="crawl-guide-answer-label">{answerSourceLabel(answerUsedModel)}</span>
              <p>{answer}</p>
              {degradedHint ? <p className="hint">{degradedHint}</p> : null}
            </div>
          ) : null}

          <form className="crawl-guide-ask" onSubmit={onAsk}>
            <label className="field">
              <span>Ask about this step</span>
              <input
                value={question}
                onChange={(e) => setQuestion(e.target.value)}
                placeholder="Is depth 2 enough for this site?"
              />
            </label>
            <button type="submit" className="btn btn-ghost" disabled={loading || !question.trim()}>
              Ask guide
            </button>
          </form>

          {step >= 1 && url.trim() ? (
            <button type="button" className="btn btn-ghost crawl-guide-refresh" onClick={() => void refresh()}>
              Re-check site
            </button>
          ) : null}
        </div>
      ) : null}
    </aside>
  )
}

function MotionDot({ online }: { online: boolean | null }) {
  return (
    <span
      className={`crawl-guide-pulse${online === false ? ' is-offline' : online ? ' is-online' : ''}`}
      aria-hidden="true"
    />
  )
}

function friendlyStepCopy(step: number): string {
  const copy = [
    'Trailmap maps internal HTML links from your start URL and scores each page with Domain Link Ratio — all math runs in .NET.',
    'We will fetch the seed from the server (not your browser) and tell you if it looks reachable.',
    'Depth controls how many link hops we follow; max pages caps cost and queue size.',
    'Review pricing, then enqueue the job on RabbitMQ.',
  ]
  return copy[step] ?? copy[0]
}
