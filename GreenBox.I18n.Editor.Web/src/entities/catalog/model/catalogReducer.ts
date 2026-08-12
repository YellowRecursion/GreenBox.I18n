import type { CatalogAction, CatalogState } from './catalog'

export const initialCatalogState: CatalogState = { status: 'unavailable' }

export function catalogReducer(state: CatalogState, action: CatalogAction): CatalogState {
  switch (action.type) {
    case 'unavailable':
      return { status: 'unavailable' }
    case 'load_started':
      return state.status === 'ready' && state.catalogPath === action.catalogPath
        ? { ...state, isRefreshing: true, refreshError: undefined }
        : { status: 'loading' }
    case 'loaded':
      return {
        status: 'ready',
        catalog: action.catalog,
        catalogPath: action.catalogPath,
        isRefreshing: false,
        externalRefreshRevision: state.status === 'ready' ? state.externalRefreshRevision : undefined,
      }
    case 'externally_loaded':
      return {
        status: 'ready',
        catalog: action.catalog,
        catalogPath: action.catalogPath,
        isRefreshing: false,
        externalRefreshRevision: action.catalog.revision,
      }
    case 'failed':
      return state.status === 'ready'
        ? { ...state, isRefreshing: false, refreshError: action.message }
        : { status: 'error', message: action.message }
    default:
      return state
  }
}
