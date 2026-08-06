import { postJson } from '../../../shared/api/httpClient'
import type { CatalogLocale, CatalogSnapshot } from '../model/catalog'

export interface CatalogLocaleRename {
  fromId: string
  toId: string
}

export function applyCatalogLocales(
  locales: CatalogLocale[],
  defaultLocale: string,
  expectedRevision: number,
  renames: CatalogLocaleRename[] = [],
  removedIds: string[] = [],
) {
  return postJson<CatalogSnapshot>('/api/catalog/locales/apply', {
    expectedRevision,
    defaultLocale,
    locales,
    renames,
    removedIds,
  })
}
