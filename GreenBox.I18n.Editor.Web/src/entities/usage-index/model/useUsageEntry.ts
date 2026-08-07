import { useEffect, useState } from 'react'
import { getUsageEntry, type UsageEntry } from '../api/getUsageEntry'

export type UsageEntryState =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; entry: UsageEntry }
  | { status: 'error'; message: string }

export function useUsageEntry(
  entryId: string,
  indexRevision: string | undefined,
  enabled: boolean,
): UsageEntryState {
  const [state, setState] = useState<UsageEntryState>({ status: 'idle' })

  useEffect(() => {
    if (!enabled || !indexRevision) {
      setState({ status: 'idle' })
      return
    }

    const abortController = new AbortController()
    setState({ status: 'loading' })
    getUsageEntry(entryId, abortController.signal)
      .then((entry) => {
        if (!abortController.signal.aborted) {
          setState(entry.availability === 'available'
            ? { status: 'ready', entry }
            : { status: 'error', message: 'Usage locations are unavailable.' })
        }
      })
      .catch((error: unknown) => {
        if (!abortController.signal.aborted) {
          setState({
            status: 'error',
            message: error instanceof Error ? error.message : 'Usage locations could not be loaded.',
          })
        }
      })

    return () => abortController.abort()
  }, [enabled, entryId, indexRevision])

  return state
}
