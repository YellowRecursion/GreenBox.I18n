import { postJson } from '../../../shared/api/httpClient'

export interface OpenUsageResponse {
  status: 'opened' | 'requiresUserAction' | 'notFound' | 'failed'
  message: string | null
}

export function openUsage(entryId: string, locationId: string) {
  return postJson<OpenUsageResponse>('/api/usage-index/open', { entryId, locationId })
}
