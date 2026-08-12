import { useCallback, useEffect, useMemo, useReducer, type PropsWithChildren } from 'react'
import { getCatalogSession } from '../api/getCatalogSession'
import { openCatalog as requestOpenCatalog } from '../api/openCatalog'
import { CatalogSessionContext } from './catalogSessionContext'
import { catalogSessionReducer, initialCatalogSessionState } from './catalogSessionReducer'

export function CatalogSessionProvider({ children }: PropsWithChildren) {
  const [state, dispatch] = useReducer(catalogSessionReducer, initialCatalogSessionState)

  useEffect(() => {
    const abortController = new AbortController()
    let requestPending = false

    const loadInitial = getCatalogSession(abortController.signal)
      .then((snapshot) => dispatch({ type: 'loaded', snapshot }))
      .catch((error: unknown) => {
        if (!abortController.signal.aborted) {
          const message = error instanceof Error ? error.message : 'Unknown error.'
          dispatch({ type: 'failed', message })
        }
      })

    const refresh = async () => {
      if (requestPending || abortController.signal.aborted || document.hidden) return
      requestPending = true
      try {
        const snapshot = await getCatalogSession(abortController.signal)
        dispatch({ type: 'refreshed', snapshot })
      } catch {
        // A transient background failure must not replace the working editor with an error page.
      } finally {
        requestPending = false
      }
    }

    const timer = window.setInterval(() => void refresh(), 1000)
    const refreshWhenVisible = () => {
      if (!document.hidden) void refresh()
    }
    document.addEventListener('visibilitychange', refreshWhenVisible)

    return () => {
      void loadInitial
      window.clearInterval(timer)
      document.removeEventListener('visibilitychange', refreshWhenVisible)
      abortController.abort()
    }
  }, [])

  const openCatalog = useCallback(async (path: string) => {
    dispatch({ type: 'open_started' })

    try {
      const snapshot = await requestOpenCatalog(path)
      dispatch({ type: 'open_succeeded', snapshot })
      return true
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'Unknown error.'
      dispatch({ type: 'open_failed', message })
      return false
    }
  }, [])

  const dismissOperationError = useCallback(() => {
    dispatch({ type: 'operation_error_dismissed' })
  }, [])

  const context = useMemo(
    () => ({ state, openCatalog, dismissOperationError }),
    [state, openCatalog, dismissOperationError],
  )

  return (
    <CatalogSessionContext.Provider value={context}>
      {children}
    </CatalogSessionContext.Provider>
  )
}
