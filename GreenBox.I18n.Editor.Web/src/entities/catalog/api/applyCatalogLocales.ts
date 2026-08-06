import { postJson } from '../../../shared/api/httpClient'
import type { CatalogLocale, CatalogSnapshot } from '../model/catalog'

export function applyCatalogLocales(
  locales: CatalogLocale[],
  defaultLocale: string,
  expectedRevision: number,
) {
  return postJson<CatalogSnapshot>('/api/catalog/locales/apply', {
    expectedRevision,
    defaultLocale,
    locales,
  })
}
