import { getJson, postJson } from '../../../shared/api/httpClient'

export interface EditorPreferences {
  reopenLastCatalog: boolean
  lastCatalogPath: string | null
  restoreError: string | null
}

export function getEditorPreferences(signal?: AbortSignal) {
  return getJson<EditorPreferences>('/api/preferences', signal)
}

export function updateEditorPreferences(reopenLastCatalog: boolean) {
  return postJson<EditorPreferences>('/api/preferences', { reopenLastCatalog })
}
