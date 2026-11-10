/**
 * Assistive AI panels for Crawl Job Details.
 * Mount from Job Details when /crawl/jobs/:id exists. Feature-flag gated.
 * AI never owns link parsing / Domain Link Ratio — ratios are display-only from API.
 */
import { useEffect, useState } from 'react'
import {
  askTheCrawl,
  confirmAgent,
  confirmLoginStep,
  detectLogin,
  fetchAiFeatures,
  planAgent,
  suggestFields,
  summarizeJob,
  type AiFeatureFlags,
  type CrawlPageSnapshot,
  type FieldSuggestion,
} from './crawlAiApi'
import './ai-assist.css'

type Props = {
  jobId: string
  seedUrl?: string
  status?: string
  pages?: CrawlPageSnapshot[]
  pageCount?: number
  edgeCount?: number
  /** Optional HTML snapshot for field finder / login detect (operator-supplied). */
  htmlSnapshot?: string
}

export function AiAssistPanels(props: Props) {
  const [flags, setFlags] = useState<AiFeatureFlags | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    fetchAiFeatures()
      .then((f) => {
        if (!cancelled) setFlags(f)
      })
      .catch((e: Error) => {
        if (!cancelled) setError(e.message)
      })
    return () => {
      cancelled = true
    }
  }, [])

  if (error) {
    return (
      <section className="crawl-ai" aria-label="Assistive AI">
        <p className="crawl-ai__muted">AI overlays unavailable: {error}</p>
      </section>
    )
  }

  if (!flags) {
    return (
      <section className="crawl-ai" aria-label="Assistive AI">
        <p className="crawl-ai__muted">Checking AI feature flags…</p>
      </section>
    )
  }

  if (!flags.enabled) {
    return null
  }

  return (
    <section className="crawl-ai" aria-label="Assistive AI overlays">
      <header className="crawl-ai__header">
        <h2>Assistive AI</h2>
        <p>
          Operator confirmations required. Crawl graph math stays in .NET
          {flags.deepSeekConfigured ? '' : ' · DeepSeek key missing (heuristic mode)'}.
        </p>
      </header>

      {flags.fieldFinderEnabled && (
        <FieldFinderPanel jobId={props.jobId} htmlSnapshot={props.htmlSnapshot} />
      )}
      {flags.loginAssistEnabled && (
        <LoginAssistPanel jobId={props.jobId} htmlSnapshot={props.htmlSnapshot} />
      )}
      {flags.summarizationEnabled && (
        <SummarizePanel
          jobId={props.jobId}
          seedUrl={props.seedUrl}
          status={props.status}
          pages={props.pages}
          pageCount={props.pageCount}
          edgeCount={props.edgeCount}
        />
      )}
      {flags.askTheCrawlEnabled && (
        <AskPanel jobId={props.jobId} pages={props.pages ?? []} />
      )}
      {flags.agentLoopEnabled && <AgentPanel jobId={props.jobId} />}
    </section>
  )
}

function FieldFinderPanel({
  jobId,
  htmlSnapshot,
}: {
  jobId: string
  htmlSnapshot?: string
}) {
  const [html, setHtml] = useState(htmlSnapshot ?? '')
  const [suggestions, setSuggestions] = useState<FieldSuggestion[]>([])
  const [note, setNote] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function run() {
    setBusy(true)
    setNote(null)
    try {
      const resp = await suggestFields(jobId, {
        htmlOrText: html,
        goalHint: 'eligibility claim member npi',
      })
      setSuggestions(resp.suggestions)
      setNote(
        resp.usedModel
          ? 'Model suggestions — confirm before saving extraction hints.'
          : resp.degradedReason ?? 'Heuristic suggestions — confirm before use.',
      )
    } catch (e) {
      setNote((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="crawl-ai__block">
      <h3>Field / data finder</h3>
      <textarea
        value={html}
        onChange={(e) => setHtml(e.target.value)}
        placeholder="Paste page HTML/text snapshot"
        rows={4}
      />
      <button type="button" disabled={busy || !html.trim()} onClick={() => void run()}>
        Suggest fields
      </button>
      {note && <p className="crawl-ai__muted">{note}</p>}
      <ul>
        {suggestions.map((s) => (
          <li key={`${s.label}-${s.cssSelector}`}>
            <strong>{s.label}</strong>
            {s.cssSelector ? ` · ${s.cssSelector}` : ''} — {s.reason} ({s.confidence.toFixed(2)})
          </li>
        ))}
      </ul>
    </div>
  )
}

function LoginAssistPanel({
  jobId,
  htmlSnapshot,
}: {
  jobId: string
  htmlSnapshot?: string
}) {
  const [html, setHtml] = useState(htmlSnapshot ?? '')
  const [auditEventId, setAuditEventId] = useState<string | null>(null)
  const [steps, setSteps] = useState<string[]>([])
  const [vaultRef, setVaultRef] = useState('vault:payer/demo')
  const [message, setMessage] = useState<string | null>(null)

  async function detect() {
    setMessage(null)
    const resp = await detectLogin(jobId, { htmlOrText: html })
    setAuditEventId(resp.auditEventId)
    setSteps(resp.detection.guidedSteps)
    setMessage(
      resp.detection.detected
        ? 'Login form detected. Approve steps; never paste passwords.'
        : 'No login form detected.',
    )
  }

  async function approve(stepIndex: number) {
    if (!auditEventId) return
    const resp = await confirmLoginStep(jobId, {
      auditEventId,
      stepIndex,
      approved: true,
      credentialVaultRef: vaultRef,
      operatorId: 'ui-operator',
    })
    setMessage(resp.message)
  }

  return (
    <div className="crawl-ai__block">
      <h3>Assisted login</h3>
      <textarea
        value={html}
        onChange={(e) => setHtml(e.target.value)}
        placeholder="Paste login page HTML"
        rows={3}
      />
      <label className="crawl-ai__label">
        Credential vault ref (never a password)
        <input value={vaultRef} onChange={(e) => setVaultRef(e.target.value)} />
      </label>
      <button type="button" onClick={() => void detect().catch((e) => setMessage(String(e)))}>
        Detect form
      </button>
      {message && <p className="crawl-ai__muted">{message}</p>}
      <ol>
        {steps.map((s, i) => (
          <li key={s}>
            {s}{' '}
            <button type="button" onClick={() => void approve(i).catch((e) => setMessage(String(e)))}>
              Approve step
            </button>
          </li>
        ))}
      </ol>
    </div>
  )
}

function SummarizePanel(props: {
  jobId: string
  seedUrl?: string
  status?: string
  pages?: CrawlPageSnapshot[]
  pageCount?: number
  edgeCount?: number
}) {
  const [summary, setSummary] = useState<string | null>(null)
  const [highlights, setHighlights] = useState<string[]>([])

  async function run() {
    const resp = await summarizeJob(props.jobId, {
      seedUrl: props.seedUrl ?? '',
      status: props.status ?? 'Unknown',
      pageCount: props.pageCount ?? props.pages?.length ?? 0,
      edgeCount: props.edgeCount ?? 0,
      pages: props.pages ?? [],
    })
    setSummary(resp.summary)
    setHighlights(resp.highlights)
  }

  return (
    <div className="crawl-ai__block">
      <h3>Result summary</h3>
      <button type="button" onClick={() => void run().catch((e) => setSummary(String(e)))}>
        Summarize job
      </button>
      {summary && <p>{summary}</p>}
      <ul>
        {highlights.map((h) => (
          <li key={h}>{h}</li>
        ))}
      </ul>
    </div>
  )
}

function AskPanel({ jobId, pages }: { jobId: string; pages: CrawlPageSnapshot[] }) {
  const [q, setQ] = useState('which pages have ratio < 0.2?')
  const [answer, setAnswer] = useState<string | null>(null)
  const [citations, setCitations] = useState<{ url: string; snippet: string }[]>([])

  async function run() {
    const resp = await askTheCrawl(jobId, { question: q, pages })
    setAnswer(resp.answer)
    setCitations(resp.citations)
  }

  return (
    <div className="crawl-ai__block">
      <h3>Ask the crawl</h3>
      <input value={q} onChange={(e) => setQ(e.target.value)} aria-label="Question" />
      <button type="button" onClick={() => void run().catch((e) => setAnswer(String(e)))}>
        Ask
      </button>
      {answer && <p>{answer}</p>}
      <ul>
        {citations.map((c) => (
          <li key={c.url}>
            <a href={c.url}>{c.url}</a> — {c.snippet}
          </li>
        ))}
      </ul>
    </div>
  )
}

function AgentPanel({ jobId }: { jobId: string }) {
  const [planId, setPlanId] = useState<string | null>(null)
  const [role, setRole] = useState<string | null>(null)
  const [steps, setSteps] = useState<string[]>([])
  const [tools, setTools] = useState<string[]>([])
  const [result, setResult] = useState<string | null>(null)

  async function plan() {
    const resp = await planAgent(jobId, { goal: 'propose eligibility fields and summarize' })
    setPlanId(resp.planId)
    setRole(resp.roleSignal)
    setSteps(resp.planSteps)
    setTools(resp.proposedToolCalls.map((t) => t.toolName))
    setResult('Plan ready — human confirm required before tools run.')
  }

  async function confirm(approved: boolean) {
    if (!planId) return
    const resp = await confirmAgent(jobId, {
      planId,
      approved,
      operatorId: 'ui-operator',
      approvedToolNames: tools,
    })
    setResult(`${resp.status}: ${resp.results.join(' | ')}`)
  }

  return (
    <div className="crawl-ai__block">
      <h3>Agent loop (human confirm)</h3>
      {role && <p className="crawl-ai__muted">{role}</p>}
      <button type="button" onClick={() => void plan().catch((e) => setResult(String(e)))}>
        Propose plan
      </button>
      <ol>
        {steps.map((s) => (
          <li key={s}>{s}</li>
        ))}
      </ol>
      {planId && (
        <div className="crawl-ai__actions">
          <button type="button" onClick={() => void confirm(true)}>
            Approve tools
          </button>
          <button type="button" onClick={() => void confirm(false)}>
            Decline
          </button>
        </div>
      )}
      {result && <p>{result}</p>}
    </div>
  )
}
