/**
 * Beginner-hidden drawer — documents worker options; persists prefs for element pick + retries.
 */
import { useEffect, useId, useState } from 'react'
import {
  DEFAULT_ADVANCED,
  type CrawlAdvancedOptions,
  saveAdvancedToStorage,
} from './crawlAdvancedOptions'

type Props = {
  open: boolean
  onClose: () => void
  value: CrawlAdvancedOptions
  onChange: (next: CrawlAdvancedOptions) => void
}

export function AdvancedOptionsDrawer({ open, onClose, value, onChange }: Props) {
  const titleId = useId()
  const [draft, setDraft] = useState(value)

  useEffect(() => {
    if (open) setDraft(value)
  }, [open, value])

  if (!open) return null

  const patch = (partial: Partial<CrawlAdvancedOptions>) => setDraft((d) => ({ ...d, ...partial }))

  const save = () => {
    onChange(draft)
    saveAdvancedToStorage(draft)
    onClose()
  }

  return (
    <div className="crawl-drawer-backdrop" role="presentation" onClick={onClose}>
      <div
        className="crawl-drawer"
        role="dialog"
        aria-labelledby={titleId}
        onClick={(e) => e.stopPropagation()}
      >
        <header className="crawl-drawer-head">
          <h2 id={titleId}>Advanced options</h2>
          <p className="hint">
            Hidden from beginners — tune limits and note login/selector prefs. Robots and politeness are enforced
            in the .NET worker; toggles here set expectations before you crawl.
          </p>
          <button type="button" className="btn btn-ghost crawl-drawer-close" onClick={onClose}>
            Close
          </button>
        </header>

        <div className="crawl-drawer-body">
          <label className="field">
            <span>Max depth (0–5)</span>
            <input
              type="number"
              min={0}
              max={5}
              value={draft.maxDepth}
              onChange={(e) => patch({ maxDepth: Number(e.target.value) })}
            />
          </label>
          <p className="hint">How many link hops from the seed URL.</p>

          <label className="field">
            <span>Max pages (1–200)</span>
            <input
              type="number"
              min={1}
              max={200}
              value={draft.maxPages}
              onChange={(e) => patch({ maxPages: Number(e.target.value) })}
            />
          </label>
          <p className="hint">Hard stop for queue size and Stripe test meter exposure.</p>

          <label className="field crawl-checkbox">
            <input
              type="checkbox"
              checked={draft.honorRobots}
              onChange={(e) => patch({ honorRobots: e.target.checked })}
            />
            <span>Honor robots.txt (worker default)</span>
          </label>
          <p className="hint">When off, expect faster queues but possible blocks — worker config applies.</p>

          <label className="field">
            <span>Politeness / rate note</span>
            <textarea
              rows={2}
              value={draft.politenessNote}
              onChange={(e) => patch({ politenessNote: e.target.value })}
            />
          </label>

          <label className="field">
            <span>Login credential vault reference (never the password)</span>
            <input
              value={draft.credentialVaultRef}
              onChange={(e) => patch({ credentialVaultRef: e.target.value })}
              placeholder="vault://team/demo-login"
              autoComplete="off"
            />
          </label>
          <p className="hint">Referenced on Job Details → Assisted login after the crawl starts.</p>

          <fieldset className="crawl-drawer-fieldset">
            <legend>Element picking</legend>
            <label className="field crawl-radio">
              <input
                type="radio"
                name="selectorMode"
                checked={draft.selectorMode === 'css'}
                onChange={() => patch({ selectorMode: 'css' })}
              />
              CSS selectors
            </label>
            <label className="field crawl-radio">
              <input
                type="radio"
                name="selectorMode"
                checked={draft.selectorMode === 'xpath'}
                onChange={() => patch({ selectorMode: 'xpath' })}
              />
              XPath
            </label>
            <label className="field">
              <span>Extraction goal (for AI field finder)</span>
              <input
                value={draft.elementPickGoal}
                onChange={(e) => patch({ elementPickGoal: e.target.value })}
                placeholder="e.g. book titles and prices"
              />
            </label>
          </fieldset>
        </div>

        <footer className="crawl-drawer-foot">
          <button
            type="button"
            className="btn btn-ghost"
            onClick={() => {
              const next = { ...DEFAULT_ADVANCED }
              setDraft(next)
              onChange(next)
              saveAdvancedToStorage(next)
            }}
          >
            Reset defaults
          </button>
          <button type="button" className="btn btn-primary" onClick={save}>
            Save options
          </button>
        </footer>
      </div>
    </div>
  )
}
