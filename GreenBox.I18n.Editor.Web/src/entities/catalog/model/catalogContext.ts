import { createContext } from 'react'
import type { CatalogLocale, CatalogSnapshot, CatalogState } from './catalog'
import type { CatalogEntryMove } from '../api/moveCatalogEntries'
import type { CatalogEntryDelta } from '../api/applyCatalogEntryDelta'
import type { CatalogLocaleRename } from '../api/applyCatalogLocales'

export interface CatalogContextValue {
  state: CatalogState
  addEntry(path: string): Promise<CatalogSnapshot>
  removeEntries(ids: string[]): Promise<CatalogSnapshot>
  moveEntries(moves: CatalogEntryMove[]): Promise<CatalogSnapshot>
  applyEntryDelta(delta: CatalogEntryDelta, expectedRevision: number): Promise<CatalogSnapshot>
  applyLocales(
    locales: CatalogLocale[],
    defaultLocale: string,
    expectedRevision: number,
    renames?: CatalogLocaleRename[],
    removedIds?: string[],
  ): Promise<CatalogSnapshot>
  save(overwriteExternalChanges?: boolean): Promise<CatalogSnapshot>
  revert(): Promise<CatalogSnapshot>
  mergeSource(): Promise<CatalogSnapshot>
}

export const CatalogContext = createContext<CatalogContextValue | undefined>(undefined)
