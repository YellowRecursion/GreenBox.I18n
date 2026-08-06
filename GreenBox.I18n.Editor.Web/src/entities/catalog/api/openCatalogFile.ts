import { postJson } from '../../../shared/api/httpClient'

export function openCatalogFile() {
  return postJson<{ opened: boolean }>('/api/session/open-file', {})
}
