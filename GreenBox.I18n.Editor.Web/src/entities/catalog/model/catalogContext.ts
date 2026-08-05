import { createContext } from 'react'
import type { CatalogSnapshot, CatalogState } from './catalog'
import type { CatalogEntryMove } from '../api/moveCatalogEntries'

export interface CatalogContextValue {
  state: CatalogState
  addEntry(path: string): Promise<CatalogSnapshot>
  removeEntries(ids: string[]): Promise<CatalogSnapshot>
  moveEntries(moves: CatalogEntryMove[]): Promise<CatalogSnapshot>
  save(overwriteExternalChanges?: boolean): Promise<CatalogSnapshot>
  revert(): Promise<CatalogSnapshot>
  mergeSource(): Promise<CatalogSnapshot>
}

export const CatalogContext = createContext<CatalogContextValue | undefined>(undefined)
