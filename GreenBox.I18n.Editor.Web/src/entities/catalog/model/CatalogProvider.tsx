import { useEffect, useReducer, type PropsWithChildren } from 'react'
import { getCatalog } from '../api/getCatalog'
import { CatalogContext } from './catalogContext'
import { catalogReducer, initialCatalogState } from './catalogReducer'
import { useCatalogSession } from './useCatalogSession'

export function CatalogProvider({ children }: PropsWithChildren) {
  const { state: session } = useCatalogSession()
  const [state, dispatch] = useReducer(catalogReducer, initialCatalogState)
  const sessionCatalog = session.status === 'ready' && session.snapshot.hasCatalog
    ? {
        revision: session.snapshot.revision,
        path: session.snapshot.catalogPath,
      }
    : undefined
  const revision = sessionCatalog?.revision
  const catalogPath = sessionCatalog?.path

  useEffect(() => {
    if (revision === undefined || !catalogPath) {
      dispatch({ type: 'unavailable' })
      return
    }

    const abortController = new AbortController()
    dispatch({ type: 'load_started', catalogPath })

    getCatalog(abortController.signal)
      .then((catalog) => dispatch({ type: 'loaded', catalog, catalogPath }))
      .catch((error: unknown) => {
        if (!abortController.signal.aborted) {
          const message = error instanceof Error ? error.message : 'Unknown error.'
          dispatch({ type: 'failed', message })
        }
      })

    return () => abortController.abort()
  }, [revision, catalogPath])

  return <CatalogContext.Provider value={state}>{children}</CatalogContext.Provider>
}
