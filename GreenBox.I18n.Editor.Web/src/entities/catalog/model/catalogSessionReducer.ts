import type { CatalogSessionAction, CatalogSessionSnapshot, CatalogSessionState } from './catalogSession'

export const initialCatalogSessionState: CatalogSessionState = { status: 'loading' }

export function catalogSessionReducer(
  state: CatalogSessionState,
  action: CatalogSessionAction,
): CatalogSessionState {
  switch (action.type) {
    case 'loaded':
      return { status: 'ready', snapshot: action.snapshot, isOpening: false }
    case 'refreshed':
      if (state.status !== 'ready' || areSnapshotsEqual(state.snapshot, action.snapshot)) {
        return state
      }
      return { ...state, snapshot: action.snapshot }
    case 'failed':
      return { status: 'error', message: action.message }
    case 'open_started':
      return state.status === 'ready'
        ? { ...state, isOpening: true, operationError: undefined }
        : state
    case 'open_succeeded':
      return { status: 'ready', snapshot: action.snapshot, isOpening: false }
    case 'open_failed':
      return state.status === 'ready'
        ? { ...state, isOpening: false, operationError: action.message }
        : state
    case 'operation_error_dismissed':
      return state.status === 'ready'
        ? { ...state, operationError: undefined }
        : state
    default:
      return state
  }
}

function areSnapshotsEqual(left: CatalogSessionSnapshot, right: CatalogSessionSnapshot) {
  return left.hasCatalog === right.hasCatalog &&
    left.revision === right.revision &&
    left.catalogPath === right.catalogPath &&
    left.defaultLocale === right.defaultLocale &&
    left.localeCount === right.localeCount &&
    left.entryCount === right.entryCount
}
