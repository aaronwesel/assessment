/**
 * Live job progress: prefer SignalR hub, fall back to REST polling.
 * Hub unavailable / negotiate fail → poll mode (PDF flow still works).
 */
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { useCallback, useEffect, useRef, useState } from 'react'
import type { CrawlJob, PageCounts } from '../api/crawl'
import { config } from '../config'

export type LiveTransport = 'connecting' | 'signalr' | 'polling' | 'idle'

export interface JobProgressPayload {
  jobId: string
  status: string
  progressPercent: number
  total: number
  queued: number
  processing: number
  done: number
  skipped: number
  failed: number
  startedAt?: string | null
  completedAt?: string | null
  failureReason?: string | null
  eventKind?: string
  occurredAt?: string
}

const POLL_MS = 1500
const SIGNALR_STALE_FALLBACK_MS = 8000

function applyProgress(job: CrawlJob, payload: JobProgressPayload): CrawlJob {
  const pageCounts: PageCounts = {
    total: payload.total,
    queued: payload.queued,
    processing: payload.processing,
    done: payload.done,
    skipped: payload.skipped,
    failed: payload.failed,
  }
  return {
    ...job,
    status: payload.status as CrawlJob['status'],
    progressPercent: payload.progressPercent,
    pageCounts,
    startedAt: payload.startedAt ?? job.startedAt,
    completedAt: payload.completedAt ?? job.completedAt,
    failureReason: payload.failureReason ?? job.failureReason,
  }
}

export function useJobRealtime(opts: {
  jobId: string
  job: CrawlJob | null
  setJob: (job: CrawlJob | ((prev: CrawlJob | null) => CrawlJob | null)) => void
  isLive: boolean
  refreshJob: () => Promise<unknown>
  refreshTree: () => Promise<unknown>
}): { transport: LiveTransport; pollMs: number } {
  const { jobId, job, setJob, isLive, refreshJob, refreshTree } = opts
  const [transport, setTransport] = useState<LiveTransport>('idle')
  const connectionRef = useRef<HubConnection | null>(null)
  const lastSignalRef = useRef(0)
  const hubReadyRef = useRef(false)

  const onProgress = useCallback(
    (raw: JobProgressPayload) => {
      lastSignalRef.current = Date.now()
      hubReadyRef.current = true
      setTransport('signalr')
      setJob((prev) => {
        if (!prev || prev.jobId.toLowerCase() !== String(raw.jobId).toLowerCase()) return prev
        return applyProgress(prev, {
          ...raw,
          jobId: String(raw.jobId),
        })
      })
      // Tree still via REST — SignalR carries counts/status only.
      void refreshTree()
    },
    [refreshTree, setJob],
  )

  useEffect(() => {
    if (!jobId || !isLive) {
      setTransport('idle')
      hubReadyRef.current = false
      const existing = connectionRef.current
      connectionRef.current = null
      if (existing) void existing.stop()
      return
    }

    let cancelled = false
    setTransport('connecting')
    hubReadyRef.current = false

    const hubUrl = `${config.crawlApiBaseUrl.replace(/\/$/, '')}/hubs/crawl-jobs`
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build()

    connectionRef.current = connection
    connection.on('jobProgress', onProgress)

    connection.onreconnecting(() => {
      if (!cancelled) setTransport('polling')
    })
    connection.onreconnected(() => {
      if (cancelled) return
      void connection.invoke('SubscribeJob', jobId).then(() => {
        hubReadyRef.current = true
        setTransport('signalr')
      })
    })

    ;(async () => {
      try {
        await connection.start()
        if (cancelled) return
        await connection.invoke('SubscribeJob', jobId)
        hubReadyRef.current = true
        lastSignalRef.current = Date.now()
        setTransport('signalr')
        // Bootstrap from REST once so UI is not empty before first event.
        await refreshJob()
        await refreshTree()
      } catch {
        if (!cancelled) setTransport('polling')
      }
    })()

    return () => {
      cancelled = true
      connection.off('jobProgress', onProgress)
      void connection.stop()
      if (connectionRef.current === connection) connectionRef.current = null
    }
  }, [jobId, isLive, onProgress, refreshJob, refreshTree])

  // REST fallback: always while live; slows when SignalR is healthy.
  useEffect(() => {
    if (!jobId || !isLive) return

    const timer = window.setInterval(() => {
      const conn = connectionRef.current
      const hubUp =
        hubReadyRef.current &&
        conn?.state === HubConnectionState.Connected &&
        Date.now() - lastSignalRef.current < SIGNALR_STALE_FALLBACK_MS

      if (hubUp) {
        // Soft poll tree occasionally even on SignalR (progress events already refresh tree).
        return
      }

      if (transport !== 'connecting') setTransport('polling')
      void refreshJob()
      void refreshTree()
    }, POLL_MS)

    return () => window.clearInterval(timer)
  }, [jobId, isLive, refreshJob, refreshTree, transport, job?.status])

  return { transport, pollMs: POLL_MS }
}
