import { postJson } from '../../../shared/api/httpClient'
import type { CatalogSnapshot } from '../model/catalog'

export function addCatalogEntry(path: string) {
  return postJson<CatalogSnapshot>('/api/catalog/entries', { path })
}
