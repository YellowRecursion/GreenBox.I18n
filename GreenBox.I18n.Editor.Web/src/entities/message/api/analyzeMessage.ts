import { postJson } from '../../../shared/api/httpClient'

export interface MessageAnalysis {
  isValid: boolean
  arguments: string[]
  diagnostics: MessageAnalysisDiagnostic[]
}

export interface MessageAnalysisDiagnostic {
  code: string
  message: string
  position: number
  argumentName: string | null
}

export function analyzeMessage(source: string, signal?: AbortSignal) {
  return postJson<MessageAnalysis>('/api/messages/analyze', { source }, signal)
}
