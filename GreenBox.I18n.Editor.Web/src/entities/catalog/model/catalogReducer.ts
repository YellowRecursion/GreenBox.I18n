import type { CatalogAction, CatalogState } from './catalog'

export const initialCatalogState: CatalogState = { status: 'unavailable' }

export function catalogReducer(state: CatalogState, action: CatalogAction): CatalogState {
  switch (action.type) {
    case 'unavailable':
      return { status: 'unavailable' }
    case 'load_started':
      return { status: 'loading' }
    case 'loaded':
      return { status: 'ready', catalog: action.catalog }
    case 'failed':
      return { status: 'error', message: action.message }
    default:
      return state
  }
}
