export interface CatalogSessionSnapshot {
  hasCatalog: boolean
  revision: number
}

export type CatalogSessionState =
  | { status: 'loading' }
  | { status: 'ready'; snapshot: CatalogSessionSnapshot }
  | { status: 'error'; message: string }

export type CatalogSessionAction =
  | { type: 'loaded'; snapshot: CatalogSessionSnapshot }
  | { type: 'failed'; message: string }
