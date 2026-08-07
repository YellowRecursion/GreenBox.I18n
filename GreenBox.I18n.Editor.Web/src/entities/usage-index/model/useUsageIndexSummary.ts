import { useCallback, useEffect, useRef, useState } from 'react'
import {
  getUsageIndexSummary,
  getUsageIndexState,
  type UsageIndexSummary,
} from '../api/getUsageIndexSummary'

const pollIntervalMilliseconds = 2000

export function useUsageIndexSummary(enabled: boolean) {
  const [summary, setSummary] = useState<UsageIndexSummary>()
  const isChecking = useRef(false)
  const loadedRevision = useRef<string | undefined>(undefined)

  const check = useCallback(async (signal?: AbortSignal) => {
    if (!enabled || isChecking.current) {
      return
    }

    isChecking.current = true
    try {
      const state = await getUsageIndexState(signal)
      if (state.availability !== 'available' ||
        state.status !== 'ready' ||
        state.failedSourceCount !== 0 ||
        state.updatedAtUtc === null) {
        loadedRevision.current = undefined
        setSummary(undefined)
        return
      }

      const revision = state.updatedAtUtc
      if (loadedRevision.current !== revision) {
        const nextSummary = await getUsageIndexSummary(signal)
        const summaryIsReliable = nextSummary.availability === 'available' &&
          nextSummary.status === 'ready' &&
          nextSummary.failedSourceCount === 0 &&
          nextSummary.updatedAtUtc === revision
        if (!signal?.aborted && summaryIsReliable) {
          loadedRevision.current = revision
          setSummary(nextSummary)
        }
      }
    } catch {
      // Keep the last successful snapshot across transient Host failures. Unity
      // presence independently removes it when the Editor goes offline.
    } finally {
      isChecking.current = false
    }
  }, [enabled])

  useEffect(() => {
    if (!enabled) {
      setSummary(undefined)
      loadedRevision.current = undefined
      return
    }

    const abortController = new AbortController()
    const handleFocus = () => void check()
    const handleVisibilityChange = () => {
      if (document.visibilityState === 'visible') {
        void check()
      }
    }

    void check(abortController.signal)
    const interval = window.setInterval(() => void check(), pollIntervalMilliseconds)
    window.addEventListener('focus', handleFocus)
    document.addEventListener('visibilitychange', handleVisibilityChange)

    return () => {
      abortController.abort()
      window.clearInterval(interval)
      window.removeEventListener('focus', handleFocus)
      document.removeEventListener('visibilitychange', handleVisibilityChange)
    }
  }, [check, enabled])

  return summary
}
