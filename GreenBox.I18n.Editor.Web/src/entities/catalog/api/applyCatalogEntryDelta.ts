import { postJson } from '../../../shared/api/httpClient'
import type { CatalogEntry, CatalogSnapshot } from '../model/catalog'

export interface CatalogEntryDelta {
  entries: CatalogEntry[]
  removedIds: string[]
}

export function applyCatalogEntryDelta(delta: CatalogEntryDelta, expectedRevision: number) {
  return postJson<CatalogSnapshot>('/api/catalog/entries/apply-delta', {
    expectedRevision,
    ...delta,
  })
}
