/**
 * User-facing assistive-AI status copy (no raw environment variable names).
 */

export const AI_OFFLINE_SETUP_STEPS = [
  'Copy the DeepSeek section from the repo root environment example into your local `.env` (or hosting secrets).',
  'Set the key on the Crawl API service — Docker Compose forwards it into `CrawlAi` settings; file mounts are supported.',
  'Restart or recreate the crawl-api container so it loads the new secret.',
  'Reload this page. When AI is online, answers are labeled “AI answer”; otherwise you still get labeled heuristics.',
] as const

const RAW_ENV_PATTERN = /\b(DEEPSEEK_API_KEY|CrawlAi__DeepSeekApiKey)\b/i

/** Maps legacy API degrade strings to beginner-friendly copy. */
export function formatDegradedReason(reason: string | null | undefined): string | null {
  if (!reason?.trim()) return null
  if (RAW_ENV_PATTERN.test(reason)) {
    return 'Assistive AI is offline — labeled heuristics below.'
  }
  return reason
}

export function answerSourceLabel(usedModel: boolean): string {
  return usedModel ? 'AI answer' : 'Heuristic answer'
}

export function assistiveAiHeadline(online: boolean): string {
  return online ? 'Assistive AI online' : 'Assistive AI offline'
}
