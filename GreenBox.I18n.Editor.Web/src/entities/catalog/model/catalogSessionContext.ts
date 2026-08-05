import { createContext } from 'react'
import type { CatalogSessionState } from './catalogSession'

export interface CatalogSessionContextValue {
  state: CatalogSessionState
  openCatalog(path: string): Promise<boolean>
  dismissOperationError(): void
}

export const CatalogSessionContext = createContext<CatalogSessionContextValue | undefined>(undefined)
