/**
 * Shared 1–6 step rail for guided crawl (wizard steps 1–4, job page 5–6).
 */
const STEP_LABELS = [
  'Enter website',
  'Review findings',
  'Choose scope',
  'Confirm & start',
  'Watch progress',
  'Results & export',
] as const

type Props = {
  activeStep: number
  maxReachable?: number
}

export function CrawlFlowSteps({ activeStep, maxReachable = 6 }: Props) {
  return (
    <ol className="crawl-flow-steps" aria-label="Guided crawl progress">
      {STEP_LABELS.map((label, i) => {
        const n = i + 1
        const unreachable = n > maxReachable
        const done = n < activeStep
        const current = n === activeStep
        return (
          <li
            key={label}
            className={[
              'crawl-flow-step',
              done ? 'is-done' : '',
              current ? 'is-active' : '',
              unreachable ? 'is-locked' : '',
            ]
              .filter(Boolean)
              .join(' ')}
            aria-current={current ? 'step' : undefined}
          >
            <span className="crawl-flow-num">{n}</span>
            <span className="crawl-flow-label">{label}</span>
          </li>
        )
      })}
    </ol>
  )
}

export { STEP_LABELS }
