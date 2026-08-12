import { postJson } from '../../../shared/api/httpClient'
import type { MessageAnalysisDiagnostic } from './analyzeMessage'

export type MessagePreviewValue = string | number | boolean | null

export interface MessagePreview {
  isSuccess: boolean
  text: string
  diagnostics: MessageAnalysisDiagnostic[]
}

export function previewMessage(
  source: string,
  culture: string,
  argumentsByName: Readonly<Record<string, MessagePreviewValue>>,
  signal?: AbortSignal,
) {
  return postJson<MessagePreview>('/api/messages/preview', {
    source,
    culture,
    arguments: argumentsByName,
  }, signal)
}
