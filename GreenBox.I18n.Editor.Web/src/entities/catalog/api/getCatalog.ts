import { getJson } from '../../../shared/api/httpClient'
import type { CatalogSnapshot } from '../model/catalog'

export function getCatalog(signal?: AbortSignal) {
  return getJson<CatalogSnapshot>('/api/catalog', signal)
}
