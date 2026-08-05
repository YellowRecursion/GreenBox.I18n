import type { CatalogEntryDelta } from '../../entities/catalog/api/applyCatalogEntryDelta'
import type { CatalogEntry, CatalogSnapshot } from '../../entities/catalog/model/catalog'

export interface EditorHistoryStateSnapshot {
  entryDelta: CatalogEntryDelta
  temporaryFolderPaths: string[]
}

export interface EditorHistoryEntry {
  label: string
  before: EditorHistoryStateSnapshot
  after: EditorHistoryStateSnapshot
}

export interface EditorHistoryState {
  undo: EditorHistoryEntry[]
  redo: EditorHistoryEntry[]
}

export const emptyEditorHistory: EditorHistoryState = { undo: [], redo: [] }

export function createEditorHistoryEntry(
  label: string,
  beforeCatalog: CatalogSnapshot,
  afterCatalog: CatalogSnapshot,
  beforeTemporaryFolderPaths: string[],
  afterTemporaryFolderPaths: string[],
): EditorHistoryEntry | undefined {
  const beforeById = new Map(beforeCatalog.entries.map((entry) => [entry.id, entry]))
  const afterById = new Map(afterCatalog.entries.map((entry) => [entry.id, entry]))
  const changedIds = [...new Set([...beforeById.keys(), ...afterById.keys()])]
    .filter((id) => !entriesEqual(beforeById.get(id), afterById.get(id)))

  if (changedIds.length === 0 && arraysEqual(beforeTemporaryFolderPaths, afterTemporaryFolderPaths)) {
    return undefined
  }

  return {
    label,
    before: createSnapshot(beforeById, changedIds, beforeTemporaryFolderPaths),
    after: createSnapshot(afterById, changedIds, afterTemporaryFolderPaths),
  }
}

function createSnapshot(
  entriesById: ReadonlyMap<string, CatalogEntry>,
  changedIds: string[],
  temporaryFolderPaths: string[],
): EditorHistoryStateSnapshot {
  return {
    entryDelta: {
      entries: changedIds.flatMap((id) => {
        const entry = entriesById.get(id)
        return entry ? [entry] : []
      }),
      removedIds: changedIds.filter((id) => !entriesById.has(id)),
    },
    temporaryFolderPaths: [...temporaryFolderPaths],
  }
}

function entriesEqual(left: CatalogEntry | undefined, right: CatalogEntry | undefined) {
  if (left === right) {
    return true
  }

  if (!left || !right) {
    return false
  }

  return JSON.stringify(left) === JSON.stringify(right)
}

function arraysEqual(left: string[], right: string[]) {
  return left.length === right.length && left.every((value, index) => value === right[index])
}
