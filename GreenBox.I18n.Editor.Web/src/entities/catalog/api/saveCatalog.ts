import { postJson } from '../../../shared/api/httpClient'
import type { CatalogSnapshot } from '../model/catalog'

export function saveCatalog(overwriteExternalChanges: boolean) {
  return postJson<CatalogSnapshot>('/api/catalog/save', { overwriteExternalChanges })
}
