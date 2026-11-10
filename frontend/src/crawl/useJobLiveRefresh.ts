/**
 * Job Details live refresh — SignalR push with REST poll fallback.
 * Single owner of refresh timing so layout components stay dumb.
 */
import {
  HttpTransportType,
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { useEffect, useRef, useState } from 'react'
import {
  crawlApi,
  isActiveJobStatus,
  type CrawlJob,
  type JobStatus,
  type JobTree,
  type PageCounts,
} from '../api/crawl'
import { config } from '../config'

export const JOB_DETAILS_POLL_MS = 1500
const POLL_MS_WITH_SIGNALR = 8000

export type RealtimeMode = 'connecting' | 'live' | 'polling' | 'idle'

export type JobProgressPush = {
  jobId: string
  status: JobStatus
  progressPercent: number
  pageCounts: PageCounts
  atUtc?: string
}

export type JobLiveRefreshOptions = {
  jobId: string
  job: CrawlJob | null
  setJob: (updater: (prev: CrawlJob | null) => CrawlJob | null) => void
  loadJob: (opts?: { silent?: boolean }) => Promise<CrawlJob | null>
  loadTree: (opts?: { silent?: boolean }) => Promise<void>
}

function hubUrl(): string {
  const override = import.meta.env.VITE_CRAWL_SIGNALR_URL?.trim()
  if (override) return override.replace(/\/$/, '')
  return `${config.crawlApiBaseUrl.replace(/\/$/, '')}/hubs/crawl-jobs`
}

export function useJobLiveRefresh({
  jobId,
  job,
  setJob,
  loadJob,
  loadTree,
}: JobLiveRefreshOptions): { mode: RealtimeMode } {
  const [mode, setMode] = useState<RealtimeMode>('connecting')
  const connectionRef = useRef<HubConnection | null>(null)
  const setJobRef = useRef(setJob)
  const loadTreeRef = useRef(loadTree)
  const loadJobRef = useRef(loadJob)
  setJobRef.current = setJob
  loadTreeRef.current = loadTree
  loadJobRef.current = loadJob

  const live = !!job && isActiveJobStatus(job.status)

  useEffect(() => {
    void loadJob()
    void loadTree()
  }, [jobId, loadJob, loadTree])

  useEffect(() => {
    if (!jobId) {
      setMode('connecting')
      return
    }

    if (!live) {
      setMode('idle')
      void connectionRef.current?.stop()
      connectionRef.current = null
      return
    }

    let cancelled = false
    let pollTimer: number | undefined

    const applyPush = (payload: JobProgressPush) => {
      if (!payload || payload.jobId !== jobId) return
      setJobRef.current((prev) => {
        if (!prev || prev.jobId !== jobId) return prev
        return {
          ...prev,
          status: payload.status,
          progressPercent: payload.progressPercent,
          pageCounts: payload.pageCounts,
        }
      })
      void loadTreeRef.current({ silent: true })
    }

    const startPoll = (intervalMs: number) => {
      window.clearInterval(pollTimer)
      pollTimer = window.setInterval(() => {
        void loadJobRef.current({ silent: true })
        void loadTreeRef.current({ silent: true })
      }, intervalMs)
    }

    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl(), {
        skipNegotiation: false,
        transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build()

    connectionRef.current = connection
    connection.on('jobProgress', applyPush)
    connection.onreconnecting(() => {
      if (!cancelled) {
        setMode('polling')
        startPoll(JOB_DETAILS_POLL_MS)
      }
    })
    connection.onreconnected(() => {
      if (cancelled) return
      void connection.invoke('SubscribeJob', jobId).then(() => {
        setMode('live')
        startPoll(POLL_MS_WITH_SIGNALR)
      })
    })
    connection.onclose(() => {
      if (!cancelled) {
        setMode('polling')
        startPoll(JOB_DETAILS_POLL_MS)
      }
    })

    setMode('connecting')
    startPoll(JOB_DETAILS_POLL_MS)

    void connection
      .start()
      .then(async () => {
        if (cancelled) return
        await connection.invoke('SubscribeJob', jobId)
        if (cancelled) return
        setMode('live')
        startPoll(POLL_MS_WITH_SIGNALR)
      })
      .catch(() => {
        if (!cancelled) {
          setMode('polling')
          startPoll(JOB_DETAILS_POLL_MS)
        }
      })

    return () => {
      cancelled = true
      window.clearInterval(pollTimer)
      const conn = connectionRef.current
      connectionRef.current = null
      if (conn && conn.state !== HubConnectionState.Disconnected) {
        void conn.invoke('UnsubscribeJob', jobId).catch(() => undefined)
        void conn.stop()
      }
    }
  }, [jobId, live])

  return { mode }
}

/** One-shot refresh for hub reconnect or manual "Sync" button. */
export async function refreshJobSnapshot(jobId: string): Promise<{ job: CrawlJob; tree: JobTree }> {
  const [job, tree] = await Promise.all([crawlApi.getJob(jobId), crawlApi.getJobTree(jobId)])
  return { job, tree }
}
