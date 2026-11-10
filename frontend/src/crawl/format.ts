const dateFormat = new Intl.DateTimeFormat(undefined, {
  dateStyle: 'medium',
  timeStyle: 'medium',
})

export function formatDateTime(iso: string | null | undefined): string {
  return iso ? dateFormat.format(new Date(iso)) : '—'
}

/** Domain Link Ratio as percent; null when unknown. */
export function formatPercent(ratio: number | null | undefined): string {
  if (ratio === null || ratio === undefined) return '—'
  return `${(ratio * 100).toFixed(1)}%`
}
