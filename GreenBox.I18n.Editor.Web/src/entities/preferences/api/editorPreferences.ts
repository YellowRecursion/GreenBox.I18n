import { getJson, postJson } from '../../../shared/api/httpClient'

export interface EditorPreferences {
  reopenLastCatalog: boolean
  warnUnusedEntries: boolean
  warnIncompleteEntries: boolean
  lastCatalogPath: string | null
  restoreError: string | null
}

export interface EditorPreferencesUpdate {
  reopenLastCatalog: boolean
  warnUnusedEntries: boolean
  warnIncompleteEntries: boolean
}

export function getEditorPreferences(signal?: AbortSignal) {
  return getJson<EditorPreferences>('/api/preferences', signal)
}

export function updateEditorPreferences(preferences: EditorPreferencesUpdate) {
  return postJson<EditorPreferences>('/api/preferences', preferences)
}
