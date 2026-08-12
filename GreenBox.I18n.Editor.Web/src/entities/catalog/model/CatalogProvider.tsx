import { useCallback, useEffect, useMemo, useReducer, useRef, type PropsWithChildren } from 'react'
import { addCatalogEntry } from '../api/addCatalogEntry'
import { removeCatalogEntries } from '../api/removeCatalogEntries'
import { moveCatalogEntries, type CatalogEntryMove } from '../api/moveCatalogEntries'
import { revertCatalog } from '../api/revertCatalog'
import { saveCatalog } from '../api/saveCatalog'
import { mergeCatalogSource } from '../api/mergeCatalogSource'
import { getCatalog } from '../api/getCatalog'
import { applyCatalogEntryDelta, type CatalogEntryDelta } from '../api/applyCatalogEntryDelta'
import { applyCatalogLocales, type CatalogLocaleRename } from '../api/applyCatalogLocales'
import type { CatalogLocale, CatalogSnapshot } from './catalog'
import { CatalogContext } from './catalogContext'
import { catalogReducer, initialCatalogState } from './catalogReducer'
import { useCatalogSession } from './useCatalogSession'

export function CatalogProvider({ children }: PropsWithChildren) {
  const { state: session } = useCatalogSession()
  const [state, dispatch] = useReducer(catalogReducer, initialCatalogState)
  const catalogIdentityRef = useRef<{ path: string; revision: number } | undefined>(undefined)
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

    const currentIdentity = catalogIdentityRef.current
    if (currentIdentity?.path === catalogPath && currentIdentity.revision === revision) {
      return
    }

    const abortController = new AbortController()
    const isExternalRefresh = currentIdentity !== undefined
    dispatch({ type: 'load_started', catalogPath })

    getCatalog(abortController.signal)
      .then((catalog) => {
        catalogIdentityRef.current = { path: catalogPath, revision: catalog.revision }
        dispatch({
          type: isExternalRefresh ? 'externally_loaded' : 'loaded',
          catalog,
          catalogPath,
        })
      })
      .catch((error: unknown) => {
        if (!abortController.signal.aborted) {
          const message = error instanceof Error ? error.message : 'Unknown error.'
          dispatch({ type: 'failed', message })
        }
      })

    return () => abortController.abort()
  }, [revision, catalogPath])

  const acceptCatalog = useCallback((catalog: CatalogSnapshot, path: string) => {
    catalogIdentityRef.current = { path, revision: catalog.revision }
    dispatch({ type: 'loaded', catalog, catalogPath: path })
  }, [])

  const addEntry = useCallback(async (path: string) => {
    const catalog = await addCatalogEntry(path)
    if (!catalogPath) {
      throw new Error('No catalog is open.')
    }

    acceptCatalog(catalog, catalogPath)
    return catalog
  }, [acceptCatalog, catalogPath])

  const removeEntries = useCallback(async (ids: string[]) => {
    const catalog = await removeCatalogEntries(ids)
    if (!catalogPath) {
      throw new Error('No catalog is open.')
    }

    acceptCatalog(catalog, catalogPath)
    return catalog
  }, [acceptCatalog, catalogPath])

  const moveEntries = useCallback(async (moves: CatalogEntryMove[]) => {
    const catalog = await moveCatalogEntries(moves)
    if (!catalogPath) {
      throw new Error('No catalog is open.')
    }

    acceptCatalog(catalog, catalogPath)
    return catalog
  }, [acceptCatalog, catalogPath])

  const applyEntryDelta = useCallback(async (delta: CatalogEntryDelta, expectedRevision: number) => {
    const catalog = await applyCatalogEntryDelta(delta, expectedRevision)
    if (!catalogPath) {
      throw new Error('No catalog is open.')
    }

    acceptCatalog(catalog, catalogPath)
    return catalog
  }, [acceptCatalog, catalogPath])

  const applyLocales = useCallback(async (
    locales: CatalogLocale[],
    defaultLocale: string,
    expectedRevision: number,
    renames: CatalogLocaleRename[] = [],
    removedIds: string[] = [],
  ) => {
    const catalog = await applyCatalogLocales(locales, defaultLocale, expectedRevision, renames, removedIds)
    if (!catalogPath) {
      throw new Error('No catalog is open.')
    }

    acceptCatalog(catalog, catalogPath)
    return catalog
  }, [acceptCatalog, catalogPath])

  const save = useCallback(async (overwriteExternalChanges = false) => {
    const catalog = await saveCatalog(overwriteExternalChanges)
    if (!catalogPath) {
      throw new Error('No catalog is open.')
    }

    acceptCatalog(catalog, catalogPath)
    return catalog
  }, [acceptCatalog, catalogPath])

  const revert = useCallback(async () => {
    const catalog = await revertCatalog()
    if (!catalogPath) {
      throw new Error('No catalog is open.')
    }

    acceptCatalog(catalog, catalogPath)
    return catalog
  }, [acceptCatalog, catalogPath])

  const mergeSource = useCallback(async () => {
    const catalog = await mergeCatalogSource()
    if (!catalogPath) {
      throw new Error('No catalog is open.')
    }

    acceptCatalog(catalog, catalogPath)
    return catalog
  }, [acceptCatalog, catalogPath])

  const context = useMemo(
    () => ({ state, addEntry, removeEntries, moveEntries, applyEntryDelta, applyLocales, save, revert, mergeSource }),
    [state, addEntry, removeEntries, moveEntries, applyEntryDelta, applyLocales, save, revert, mergeSource],
  )

  return <CatalogContext.Provider value={context}>{children}</CatalogContext.Provider>
}
