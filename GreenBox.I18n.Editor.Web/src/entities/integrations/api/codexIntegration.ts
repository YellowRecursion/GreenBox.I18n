import { getJson, postJson } from '../../../shared/api/httpClient'

export interface CodexIntegrationStatus {
  status: 'configured' | 'notConfigured' | 'needsRepair' | 'unavailable' | 'error'
  isConfigured: boolean
  canConfigure: boolean
  restartRequired: boolean
  message: string
  setupCommand: string | null
}

export function getCodexIntegration(signal?: AbortSignal) {
  return getJson<CodexIntegrationStatus>('/api/integrations/codex', signal)
}

export function connectCodexIntegration() {
  return postJson<CodexIntegrationStatus>('/api/integrations/codex/connect', { confirm: true })
}
