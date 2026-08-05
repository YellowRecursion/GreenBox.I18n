import type { CatalogSessionAction, CatalogSessionState } from './catalogSession'

export const initialCatalogSessionState: CatalogSessionState = { status: 'loading' }

export function catalogSessionReducer(
  state: CatalogSessionState,
  action: CatalogSessionAction,
): CatalogSessionState {
  switch (action.type) {
    case 'loaded':
      return { status: 'ready', snapshot: action.snapshot }
    case 'failed':
      return { status: 'error', message: action.message }
    default:
      return state
  }
}
