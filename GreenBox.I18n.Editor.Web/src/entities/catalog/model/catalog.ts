export interface CatalogSnapshot {
  revision: number
  defaultLocale: string
  locales: CatalogLocale[]
  entries: CatalogEntry[]
  diagnostics: CatalogDiagnostic[]
  dirtyLocaleIds: string[]
  dirtyEntryIds: string[]
  dirtyPaths?: string[]
  hasChanges: boolean
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

export interface CatalogDiagnostic {
  code: string
  severity: 'warning' | 'error'
  jsonPath: string
  message: string
  target: CatalogDiagnosticTarget | null
}

export interface CatalogDiagnosticTarget {
  entryId: string | null
  entryPath: string | null
  localeId: string | null
}

export type CatalogState =
  | { status: 'unavailable' }
  | { status: 'loading' }
  | {
      status: 'ready'
      catalog: CatalogSnapshot
      catalogPath: string
      isRefreshing: boolean
      refreshError?: string
    }
  | { status: 'error'; message: string }

export type CatalogAction =
  | { type: 'unavailable' }
  | { type: 'load_started'; catalogPath: string }
  | { type: 'loaded'; catalog: CatalogSnapshot; catalogPath: string }
  | { type: 'failed'; message: string }
