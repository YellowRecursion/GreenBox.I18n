import { getJson } from '../../../shared/api/httpClient'

export interface UnityProjectStatus {
  isUnityProject: boolean
  projectName: string | null
  projectPath: string | null
  isEditorOnline: boolean
}

export function getUnityProjectStatus(signal?: AbortSignal) {
  return getJson<UnityProjectStatus>('/api/unity-project', signal)
}
