import { useCallback, useEffect, useRef, useState } from 'react'
import {
  getUnityProjectStatus,
  type UnityProjectStatus,
} from '../api/getUnityProjectStatus'

const pollIntervalMilliseconds = 2000

export function useUnityProjectStatus() {
  const [status, setStatus] = useState<UnityProjectStatus>()
  const isChecking = useRef(false)

  const check = useCallback(async (signal?: AbortSignal) => {
    if (isChecking.current) {
      return
    }

    isChecking.current = true
    try {
      const nextStatus = await getUnityProjectStatus(signal)
      if (!signal?.aborted) {
        setStatus(nextStatus)
      }
    } catch {
      // Session-level connectivity owns Host errors. Keep the last known presence
      // so a transient request does not make the project indicator flicker.
    } finally {
      isChecking.current = false
    }
  }, [])

  useEffect(() => {
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
  }, [check])

  return status
}
