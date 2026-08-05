import { getJson, postJson } from '../../../shared/api/httpClient'

export interface ResolvedUnityAssetReference {
  assetGuid: string
  localFileId: string | null
  assetPath: string
  fileName: string
  objectName: string | null
}

export function resolveUnityAssetReference(assetGuid: string) {
  return getJson<ResolvedUnityAssetReference>(
    `/api/unity-assets/${encodeURIComponent(assetGuid)}`,
  )
}

export function resolveDroppedUnityAsset(fileName: string) {
  return postJson<ResolvedUnityAssetReference>('/api/unity-assets/resolve-drop', { fileName })
}

export function openUnityAsset(assetGuid: string) {
  return postJson<{ opened: boolean }>('/api/unity-assets/open', { assetGuid })
}
