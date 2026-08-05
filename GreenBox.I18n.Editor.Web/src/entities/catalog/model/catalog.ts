export interface CatalogSnapshot {
  revision: number
  defaultLocale: string
  locales: CatalogLocale[]
  entries: CatalogEntry[]
}

export interface CatalogLocale {
  id: string
  displayName: string
  culture: string
  fallback: string | null
  icon: CatalogAssetReference | null
}

export interface CatalogEntry {
  id: string
  path: string
  comment: string | null
  locales: Record<string, CatalogLocaleValue>
}

export interface CatalogLocaleValue {
  text: string | null
  asset: CatalogAssetReference | null
}

export interface CatalogAssetReference {
  assetGuid: string
  localFileId: string | null
}

export type CatalogState =
  | { status: 'unavailable' }
  | { status: 'loading' }
  | { status: 'ready'; catalog: CatalogSnapshot }
  | { status: 'error'; message: string }

export type CatalogAction =
  | { type: 'unavailable' }
  | { type: 'load_started' }
  | { type: 'loaded'; catalog: CatalogSnapshot }
  | { type: 'failed'; message: string }
