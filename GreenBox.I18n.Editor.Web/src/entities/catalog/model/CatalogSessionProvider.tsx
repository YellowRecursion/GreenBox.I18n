import { useEffect, useReducer, type PropsWithChildren } from 'react'
import { getCatalogSession } from '../api/getCatalogSession'
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

  return (
    <CatalogSessionContext.Provider value={state}>
      {children}
    </CatalogSessionContext.Provider>
  )
}
