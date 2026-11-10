/**
 * Element-pick export UX: choose a crawled page, suggest CSS/XPath fields, export selection.
 */
import { useMemo, useState } from 'react'
import type { PageNode } from '../../api/crawl'
import { suggestFields, type FieldSuggestion } from '../../crawl/ai/crawlAiApi'
import { flattenPageNodes } from '../../crawl/jobReport'

type Props = {
  jobId: string
  root: PageNode | null
}

type PickFormat = 'css' | 'xpath' | 'both'

export function ElementPickExport({ jobId, root }: Props) {
  const pages = useMemo(() => flattenPageNodes(root), [root])
  const [pageId, setPageId] = useState<number | ''>(pages[0]?.pageId ?? '')
  const selected = pages.find((p) => p.pageId === pageId)
  const [html, setHtml] = useState('')
  const [goal, setGoal] = useState('tables, eligibility, claim ids')
  const [suggestions, setSuggestions] = useState<FieldSuggestion[]>([])
  const [picked, setPicked] = useState<Set<string>>(new Set())
  const [format, setFormat] = useState<PickFormat>('both')
  const [note, setNote] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function runSuggest() {
    if (!html.trim()) {
      setNote('Paste HTML from the page you picked (View Source), or a snippet around the data.')
      return
    }
    setBusy(true)
    setNote(null)
    try {
      const resp = await suggestFields(jobId, {
        pageUrl: selected?.url,
        htmlOrText: html,
        goalHint: goal,
      })
      setSuggestions(resp.suggestions)
      setPicked(new Set())
      setNote(
        resp.usedModel
          ? 'Confirm selectors before export — AI does not own crawl math.'
          : resp.degradedReason ?? 'Heuristic suggestions only.',
      )
    } catch (e) {
      setNote((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  function togglePick(key: string) {
    setPicked((prev) => {
      const next = new Set(prev)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }

  function exportPicks() {
    const chosen = suggestions.filter((s) => picked.has(suggestionKey(s)))
    if (!chosen.length) {
      setNote('Select at least one field to export.')
      return
    }
    const payload = chosen.map((s) => ({
      label: s.label,
      cssSelector: format !== 'xpath' ? s.cssSelector : null,
      xpath: format !== 'css' ? s.xpath : null,
      confidence: s.confidence,
      pageUrl: selected?.url,
    }))
    const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `element-picks-${jobId.slice(0, 8)}.json`
    a.click()
    URL.revokeObjectURL(url)
    setNote(`Exported ${chosen.length} field(s).`)
  }

  return (
    <section className="crawl-panel-block crawl-element-pick" aria-labelledby="element-pick-heading">
      <h2 id="element-pick-heading" className="crawl-section-title">
        Element pick export
      </h2>
      <p className="hint">
        Pick a crawled page, paste HTML, let AI suggest CSS selectors or XPath, then export confirmed
        fields.
      </p>

      {pages.length === 0 ? (
        <p className="hint">No pages yet — start or wait for the crawl to finish.</p>
      ) : (
        <label className="field crawl-field">
          <span>Page</span>
          <select
            value={pageId}
            onChange={(e) => setPageId(e.target.value === '' ? '' : Number(e.target.value))}
          >
            {pages.map((p) => (
              <option key={p.pageId} value={p.pageId}>
                depth {p.depth} · {p.url}
              </option>
            ))}
          </select>
        </label>
      )}

      <label className="field crawl-field">
        <span>Goal hint</span>
        <input value={goal} onChange={(e) => setGoal(e.target.value)} />
      </label>

      <label className="field crawl-field">
        <span>HTML snapshot</span>
        <textarea
          rows={4}
          value={html}
          onChange={(e) => setHtml(e.target.value)}
          placeholder="Paste HTML around the data you want — open the live URL in another tab."
        />
      </label>

      <div className="crawl-export-row">
        <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void runSuggest()}>
          {busy ? 'Suggesting…' : 'Suggest fields'}
        </button>
        <label className="crawl-radio">
          <input
            type="radio"
            name="pick-format"
            checked={format === 'css'}
            onChange={() => setFormat('css')}
          />
          CSS
        </label>
        <label className="crawl-radio">
          <input
            type="radio"
            name="pick-format"
            checked={format === 'xpath'}
            onChange={() => setFormat('xpath')}
          />
          XPath
        </label>
        <label className="crawl-radio">
          <input
            type="radio"
            name="pick-format"
            checked={format === 'both'}
            onChange={() => setFormat('both')}
          />
          Both
        </label>
      </div>

      {note ? <p className="hint">{note}</p> : null}

      {suggestions.length > 0 ? (
        <ul className="crawl-pick-list">
          {suggestions.map((s) => {
            const key = suggestionKey(s)
            return (
              <li key={key}>
                <label>
                  <input
                    type="checkbox"
                    checked={picked.has(key)}
                    onChange={() => togglePick(key)}
                  />
                  <strong>{s.label}</strong>
                  {s.cssSelector ? <code>{s.cssSelector}</code> : null}
                  {s.xpath ? <code>{s.xpath}</code> : null}
                  <span className="hint">{s.reason}</span>
                </label>
              </li>
            )
          })}
        </ul>
      ) : null}

      <button
        type="button"
        className="btn btn-ghost"
        disabled={!picked.size}
        onClick={exportPicks}
      >
        Export selected fields
      </button>
    </section>
  )
}

function suggestionKey(s: FieldSuggestion) {
  return `${s.label}-${s.cssSelector ?? ''}-${s.xpath ?? ''}`
}
