import { createContext } from 'react'
import type { CatalogSessionState } from './catalogSession'

export interface CatalogSessionContextValue {
  state: CatalogSessionState
  openCatalog(path: string): Promise<void>
}

export const CatalogSessionContext = createContext<CatalogSessionContextValue | undefined>(undefined)
