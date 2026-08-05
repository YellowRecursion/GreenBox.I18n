import { createContext } from 'react'
import type { CatalogSessionState } from './catalogSession'

export const CatalogSessionContext = createContext<CatalogSessionState | undefined>(undefined)
