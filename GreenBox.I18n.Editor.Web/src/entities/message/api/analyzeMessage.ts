import { postJson } from '../../../shared/api/httpClient'

export interface MessageAnalysis {
  isValid: boolean
  arguments: MessageArgument[]
  diagnostics: MessageAnalysisDiagnostic[]
}

export interface MessageArgument {
  name: string
  kind: 'unspecified' | 'string' | 'number'
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
