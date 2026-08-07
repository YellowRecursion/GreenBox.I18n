import { useCallback, useEffect, useRef, useState } from 'react'
import {
  getCatalogSourceStatus,
  type CatalogSourceStatus,
} from '../api/getCatalogSourceStatus'

const unchangedStatus: CatalogSourceStatus = {
  hasChanged: false,
  isAvailable: true,
  message: null,
}

export function useCatalogSourceMonitor(mergeFromDisk: () => Promise<void>) {
  const [status, setStatus] = useState<CatalogSourceStatus>(unchangedStatus)
  const isChecking = useRef(false)
  const mergeFromDiskRef = useRef(mergeFromDisk)

  mergeFromDiskRef.current = mergeFromDisk

  const check = useCallback(async () => {
    if (isChecking.current) {
      return
    }

    isChecking.current = true
    try {
      const nextStatus = await getCatalogSourceStatus()
      if (nextStatus.hasChanged && nextStatus.isAvailable) {
        setStatus(nextStatus)
        try {
          await mergeFromDiskRef.current()
          setStatus(unchangedStatus)
        } catch (error: unknown) {
          // Keep the external-change warning visible when the new file cannot be loaded.
          setStatus({
            ...nextStatus,
            message: error instanceof Error ? error.message : 'The external catalog could not be merged.',
          })
        }
        return
      }

      setStatus((current) => areStatusesEqual(current, nextStatus) ? current : nextStatus)
    } catch {
      // Host connectivity is reported by the session provider. A transient polling
      // failure must not replace a known source-file status.
    } finally {
      isChecking.current = false
    }
  }, [])

  useEffect(() => {
    const handleFocus = () => void check()
    const handleVisibilityChange = () => {
      if (document.visibilityState === 'visible') {
        void check()
      }
    }

    void check()
    const interval = window.setInterval(() => void check(), 1500)
    window.addEventListener('focus', handleFocus)
    document.addEventListener('visibilitychange', handleVisibilityChange)

    return () => {
      window.clearInterval(interval)
      window.removeEventListener('focus', handleFocus)
      document.removeEventListener('visibilitychange', handleVisibilityChange)
    }
  }, [check])

  const markCurrent = useCallback(() => setStatus(unchangedStatus), [])
  return { status, check, markCurrent }
}

function areStatusesEqual(left: CatalogSourceStatus, right: CatalogSourceStatus) {
  return left.hasChanged === right.hasChanged &&
    left.isAvailable === right.isAvailable &&
    left.message === right.message
}
