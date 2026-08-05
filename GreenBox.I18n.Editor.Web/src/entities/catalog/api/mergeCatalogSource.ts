import { postJson } from '../../../shared/api/httpClient'
import type { CatalogSnapshot } from '../model/catalog'

export function mergeCatalogSource() {
  return postJson<CatalogSnapshot>('/api/catalog/merge-source', {})
}
