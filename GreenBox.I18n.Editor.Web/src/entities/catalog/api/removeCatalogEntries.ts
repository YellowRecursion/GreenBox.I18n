import { postJson } from '../../../shared/api/httpClient'
import type { CatalogSnapshot } from '../model/catalog'

export function removeCatalogEntries(ids: string[]) {
  return postJson<CatalogSnapshot>('/api/catalog/entries/remove', { ids })
}
