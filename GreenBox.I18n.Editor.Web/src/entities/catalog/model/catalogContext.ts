import { createContext } from 'react'
import type { CatalogSnapshot, CatalogState } from './catalog'

export interface CatalogContextValue {
  state: CatalogState
  addEntry(path: string): Promise<CatalogSnapshot>
}

export const CatalogContext = createContext<CatalogContextValue | undefined>(undefined)
