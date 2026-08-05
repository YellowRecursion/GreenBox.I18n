import { useCallback, useEffect, useMemo, useReducer, type PropsWithChildren } from 'react'
import { getCatalogSession } from '../api/getCatalogSession'
import { openCatalog as requestOpenCatalog } from '../api/openCatalog'
import { CatalogSessionContext } from './catalogSessionContext'
import { catalogSessionReducer, initialCatalogSessionState } from './catalogSessionReducer'

export function CatalogSessionProvider({ children }: PropsWithChildren) {
  const [state, dispatch] = useReducer(catalogSessionReducer, initialCatalogSessionState)

  useEffect(() => {
    const abortController = new AbortController()

    getCatalogSession(abortController.signal)
      .then((snapshot) => dispatch({ type: 'loaded', snapshot }))
      .catch((error: unknown) => {
        if (!abortController.signal.aborted) {
          const message = error instanceof Error ? error.message : 'Unknown error.'
          dispatch({ type: 'failed', message })
        }
      })

    return () => abortController.abort()
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
