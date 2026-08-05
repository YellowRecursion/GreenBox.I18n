import { createContext } from 'react'
import type { CatalogState } from './catalog'

export const CatalogContext = createContext<CatalogState | undefined>(undefined)
