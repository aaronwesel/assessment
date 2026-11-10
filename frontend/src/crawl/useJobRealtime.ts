/**
 * Live job progress: prefer SignalR hub, fall back to REST polling.
 * Hub unavailable / negotiate fail → poll mode (PDF flow still works).
 *
 * StrictMode note: delay start slightly so the first mount's cleanup cancels
 * before negotiate begins (avoids aborted "Failed to fetch" dead-ends).
 */
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { useEffect, useRef, useState } from 'react'
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
  const { jobId, setJob, isLive, refreshJob, refreshTree } = opts
  const [transport, setTransport] = useState<LiveTransport>('idle')
  const connectionRef = useRef<HubConnection | null>(null)
  const refreshJobRef = useRef(refreshJob)
  const refreshTreeRef = useRef(refreshTree)
  const setJobRef = useRef(setJob)
  refreshJobRef.current = refreshJob
  refreshTreeRef.current = refreshTree
  setJobRef.current = setJob

  useEffect(() => {
    if (!jobId || !isLive) {
      setTransport('idle')
      const existing = connectionRef.current
      connectionRef.current = null
      if (existing) void existing.stop()
      return
    }

    let cancelled = false
    setTransport('connecting')

    const hubUrl = `${config.crawlApiBaseUrl.replace(/\/$/, '')}/hubs/crawl-jobs`
    const connection = new HubConnectionBuilder()
      // Anonymous hub + API CORS AllowAnyOrigin cannot pair with credentials:include.
      .withUrl(hubUrl, { withCredentials: false })
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build()

    connectionRef.current = connection

    const onProgress = (raw: JobProgressPayload) => {
      setTransport('signalr')
      setJobRef.current((prev) => {
        if (!prev || prev.jobId.toLowerCase() !== String(raw.jobId).toLowerCase()) return prev
        return applyProgress(prev, { ...raw, jobId: String(raw.jobId) })
      })
      void refreshTreeRef.current()
    }

    connection.on('jobProgress', onProgress)
    connection.onreconnecting(() => {
      if (!cancelled) setTransport('polling')
    })
    connection.onreconnected(() => {
      if (cancelled) return
      void connection.invoke('SubscribeJob', jobId).then(() => setTransport('signalr'))
    })

    ;(async () => {
      // Survive React StrictMode double-invoke: first mount cleanup cancels before negotiate.
      await new Promise((r) => setTimeout(r, 75))
      if (cancelled) return

      let lastError: unknown
      for (let attempt = 1; attempt <= 3; attempt++) {
        if (cancelled) return
        try {
          if (connection.state !== HubConnectionState.Disconnected) {
            await connection.stop()
          }
          await connection.start()
          if (cancelled) {
            await connection.stop()
            return
          }
          await connection.invoke('SubscribeJob', jobId)
          setTransport('signalr')
          await refreshJobRef.current()
          await refreshTreeRef.current()
          return
        } catch (err) {
          lastError = err
          await new Promise((r) => setTimeout(r, 150 * attempt))
        }
      }

      if (!cancelled) {
        console.warn('SignalR unavailable; using REST polling', lastError)
        setTransport('polling')
      }
    })()

    return () => {
      cancelled = true
      connection.off('jobProgress', onProgress)
      void connection.stop()
      if (connectionRef.current === connection) connectionRef.current = null
    }
  }, [jobId, isLive])

  // REST fallback always available while live (PDF path). When SignalR is connected,
  // keep a slower soft poll for tree consistency; badge stays SignalR.
  useEffect(() => {
    if (!jobId || !isLive) return

    const timer = window.setInterval(() => {
      const conn = connectionRef.current
      const hubConnected = conn?.state === HubConnectionState.Connected
      if (hubConnected) {
        setTransport((t) => (t === 'signalr' ? t : 'signalr'))
        // Soft tree refresh only — counts come from hub events.
        void refreshTreeRef.current()
        return
      }
      setTransport((t) => (t === 'connecting' ? t : 'polling'))
      void refreshJobRef.current()
      void refreshTreeRef.current()
    }, POLL_MS)

    return () => window.clearInterval(timer)
  }, [jobId, isLive])

  return { transport, pollMs: POLL_MS }
}
