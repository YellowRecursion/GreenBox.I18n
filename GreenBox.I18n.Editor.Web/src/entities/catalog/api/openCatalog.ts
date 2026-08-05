import { postJson } from '../../../shared/api/httpClient'
import type { CatalogSessionSnapshot } from '../model/catalogSession'

export function openCatalog(path: string) {
  return postJson<CatalogSessionSnapshot>('/api/session/open', { path })
}
