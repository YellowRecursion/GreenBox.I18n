import { getJson } from '../../../shared/api/httpClient'

export interface CatalogSourceStatus {
  hasChanged: boolean
  isAvailable: boolean
  message: string | null
}

export function getCatalogSourceStatus(signal?: AbortSignal) {
  return getJson<CatalogSourceStatus>('/api/catalog/source-status', signal)
}
