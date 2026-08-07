import { getJson } from '../../../shared/api/httpClient'

export interface UsageEntrySummary {
  entryId: string
  totalCount: number
  codeCount: number
  assetCount: number
}

export interface UsageIndexState {
  availability: string
  status: string | null
  updatedAtUtc: string | null
  lastError: string | null
  failedSourceCount: number
}

export interface UsageIndexSummary extends UsageIndexState {
  entries: UsageEntrySummary[]
}

export function getUsageIndexState(signal?: AbortSignal) {
  return getJson<UsageIndexState>('/api/usage-index/state', signal)
}

export function getUsageIndexSummary(signal?: AbortSignal) {
  return getJson<UsageIndexSummary>('/api/usage-index', signal)
}
