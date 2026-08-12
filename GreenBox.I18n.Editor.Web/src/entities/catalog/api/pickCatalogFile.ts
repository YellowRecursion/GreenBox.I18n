import { postJson } from '../../../shared/api/httpClient'

interface PickCatalogFileResponse {
  path: string | null
}

export async function pickCatalogFile() {
  const response = await postJson<PickCatalogFileResponse>('/api/session/pick-file', {})
  return response.path
}
