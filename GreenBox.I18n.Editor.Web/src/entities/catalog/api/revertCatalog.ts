import { postJson } from '../../../shared/api/httpClient'
import type { CatalogSnapshot } from '../model/catalog'

export function revertCatalog() {
  return postJson<CatalogSnapshot>('/api/catalog/revert', {})
}
