import { useContext } from 'react'
import { CatalogSessionContext } from './catalogSessionContext'

export function useCatalogSession() {
  const context = useContext(CatalogSessionContext)

  if (!context) {
    throw new Error('useCatalogSession must be used within CatalogSessionProvider.')
  }

  return context
}
