import { postJson } from '../../../shared/api/httpClient'
import type { CatalogSnapshot } from '../model/catalog'

export interface CatalogEntryMove {
  id: string
  path: string
}

export function moveCatalogEntries(moves: CatalogEntryMove[]) {
  return postJson<CatalogSnapshot>('/api/catalog/entries/move', { moves })
}
