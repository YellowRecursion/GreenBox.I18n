import { useCallback, useEffect, useMemo, useReducer, type PropsWithChildren } from 'react'
import { addCatalogEntry } from '../api/addCatalogEntry'
import { removeCatalogEntries } from '../api/removeCatalogEntries'
import { revertCatalog } from '../api/revertCatalog'
import { saveCatalog } from '../api/saveCatalog'
import { mergeCatalogSource } from '../api/mergeCatalogSource'
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

  const addEntry = useCallback(async (path: string) => {
    const catalog = await addCatalogEntry(path)
    if (!catalogPath) {
      throw new Error('No catalog is open.')
    }

    dispatch({ type: 'loaded', catalog, catalogPath })
    return catalog
  }, [catalogPath])

  const removeEntries = useCallback(async (ids: string[]) => {
    const catalog = await removeCatalogEntries(ids)
    if (!catalogPath) {
      throw new Error('No catalog is open.')
    }

    dispatch({ type: 'loaded', catalog, catalogPath })
    return catalog
  }, [catalogPath])

  const save = useCallback(async (overwriteExternalChanges = false) => {
    const catalog = await saveCatalog(overwriteExternalChanges)
    if (!catalogPath) {
      throw new Error('No catalog is open.')
    }

    dispatch({ type: 'loaded', catalog, catalogPath })
    return catalog
  }, [catalogPath])

  const revert = useCallback(async () => {
    const catalog = await revertCatalog()
    if (!catalogPath) {
      throw new Error('No catalog is open.')
    }

    dispatch({ type: 'loaded', catalog, catalogPath })
    return catalog
  }, [catalogPath])

  const mergeSource = useCallback(async () => {
    const catalog = await mergeCatalogSource()
    if (!catalogPath) {
      throw new Error('No catalog is open.')
    }

    dispatch({ type: 'loaded', catalog, catalogPath })
    return catalog
  }, [catalogPath])

  const context = useMemo(
    () => ({ state, addEntry, removeEntries, save, revert, mergeSource }),
    [state, addEntry, removeEntries, save, revert, mergeSource],
  )

  return <CatalogContext.Provider value={context}>{children}</CatalogContext.Provider>
}
