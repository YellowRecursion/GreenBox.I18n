import { getJson } from '../../../shared/api/httpClient'
import type { CatalogSessionSnapshot } from '../model/catalogSession'

export function getCatalogSession(signal?: AbortSignal) {
  return getJson<CatalogSessionSnapshot>('/api/session', signal)
}
