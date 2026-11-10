/**
 * Job Details live data refresh — REST poll today; SignalR push tomorrow.
 *
 * Elastic SignalR integration (sibling worker): subscribe to hub events and call
 * `applyJobPatch` / `applyTreePatch` instead of (or in addition to) polling.
 * Keep this hook as the single owner of refresh timing so layout components stay dumb.
 *
 * Suggested hub events (API contract TBD):
 * - `JobProgress` → partial CrawlJob fields (status, pageCounts, progressPercent)
 * - `TreeDelta`   → optional subtree merge or full tree replace (jobId must match)
 *
 * When `VITE_CRAWL_SIGNALR_URL` is set and hub connects, set polling off via env
 * `VITE_CRAWL_POLL_FALLBACK_MS` (0 = poll disabled when SignalR healthy).
 */
import { useEffect, useRef } from 'react'
import { crawlApi, isActiveJobStatus, type CrawlJob, type JobTree } from '../api/crawl'

export const JOB_DETAILS_POLL_MS = 1500

export type JobLiveRefreshOptions = {
  jobId: string
  liveStatus: CrawlJob['status'] | undefined
  loadJob: (opts?: { silent?: boolean }) => Promise<CrawlJob | null>
  loadTree: (opts?: { silent?: boolean }) => Promise<void>
  /** SignalR: pass patches from hub handler to skip a poll round-trip. */
  applyJobPatch?: (patch: Partial<CrawlJob>) => void
  applyTreePatch?: (next: JobTree) => void
}

export function useJobLiveRefresh({
  jobId,
  liveStatus,
  loadJob,
  loadTree,
}: JobLiveRefreshOptions): void {
  const signalrWanted = Boolean(import.meta.env.VITE_CRAWL_SIGNALR_URL?.trim())
  const pollFallbackMs = Number(import.meta.env.VITE_CRAWL_POLL_FALLBACK_MS ?? JOB_DETAILS_POLL_MS)
  const connectedRef = useRef(false)

  useEffect(() => {
    void loadJob()
    void loadTree()
  }, [jobId, loadJob, loadTree])

  // --- SignalR hook point (implement in dedicated module when hub ships) ---
  useEffect(() => {
    if (!signalrWanted || !jobId) return undefined
    // Example future wiring:
    // const conn = connectCrawlJobHub(import.meta.env.VITE_CRAWL_SIGNALR_URL!, jobId);
    // conn.on('JobProgress', (p) => applyJobPatch?.(p));
    // conn.on('TreeUpdated', (t) => applyTreePatch?.(t));
    // conn.start().then(() => { connectedRef.current = true; });
    // return () => { void conn.stop(); connectedRef.current = false; };
    return undefined
  }, [jobId, signalrWanted])

  useEffect(() => {
    if (!liveStatus || !isActiveJobStatus(liveStatus)) return undefined
    const pollMs =
      signalrWanted && connectedRef.current && pollFallbackMs === 0
        ? null
        : pollFallbackMs || JOB_DETAILS_POLL_MS
    if (pollMs == null) return undefined
    const timer = window.setInterval(() => {
      void loadJob({ silent: true })
      void loadTree({ silent: true })
    }, pollMs)
    return () => window.clearInterval(timer)
  }, [liveStatus, loadJob, loadTree, pollFallbackMs, signalrWanted])
}

/** One-shot refresh for hub reconnect or manual "Sync" button. */
export async function refreshJobSnapshot(jobId: string): Promise<{ job: CrawlJob; tree: JobTree }> {
  const [job, tree] = await Promise.all([crawlApi.getJob(jobId), crawlApi.getJobTree(jobId)])
  return { job, tree }
}
