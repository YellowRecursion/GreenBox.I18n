import { getJson } from '../../../shared/api/httpClient'

export interface CodeUsage {
  assembly: string
  filePath: string
  line: number
}

export interface AssetUsage {
  assetPath: string
  assetGuid: string | null
  assetLocalId: string
  gameObjectLocalId: string | null
  objectPath: string
  componentType: string
  propertyPath: string
  line: number
  isPrefabOverride: boolean
  targetAssetGuid: string | null
  targetLocalId: string | null
}

export interface UsageEntry {
  availability: string
  entryId: string
  totalCount: number
  code: CodeUsage[]
  assets: AssetUsage[]
}

export function getUsageEntry(entryId: string, signal?: AbortSignal) {
  return getJson<UsageEntry>(`/api/usage-index/entries/${encodeURIComponent(entryId)}`, signal)
}
