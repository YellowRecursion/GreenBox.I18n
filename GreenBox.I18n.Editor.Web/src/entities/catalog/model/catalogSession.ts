export interface CatalogSessionSnapshot {
  hasCatalog: boolean
  revision: number
  catalogPath: string | null
  defaultLocale: string | null
  localeCount: number
  entryCount: number
}

export type CatalogSessionState =
  | { status: 'loading' }
  | {
      status: 'ready'
      snapshot: CatalogSessionSnapshot
      isOpening: boolean
      operationError?: string
    }
  | { status: 'error'; message: string }

export type CatalogSessionAction =
  | { type: 'loaded'; snapshot: CatalogSessionSnapshot }
  | { type: 'refreshed'; snapshot: CatalogSessionSnapshot }
  | { type: 'failed'; message: string }
  | { type: 'open_started' }
  | { type: 'open_succeeded'; snapshot: CatalogSessionSnapshot }
  | { type: 'open_failed'; message: string }
  | { type: 'operation_error_dismissed' }
