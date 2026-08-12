import { useEffect, useState } from 'react'
import {
  analyzeMessage,
  type MessageAnalysis,
} from '../api/analyzeMessage'

const emptyAnalysis: MessageAnalysis = {
  isValid: true,
  arguments: [],
  diagnostics: [],
}

export interface MessageAnalysisState {
  analysis: MessageAnalysis
  analyzedSource?: string
  error?: string
}

export function useMessageAnalysis(source: string, enabled: boolean): MessageAnalysisState {
  const [state, setState] = useState<MessageAnalysisState>({ analysis: emptyAnalysis })

  useEffect(() => {
    if (!enabled) {
      setState({ analysis: emptyAnalysis })
      return
    }

    const controller = new AbortController()
    const timeout = window.setTimeout(() => {
      void analyzeMessage(source, controller.signal)
        .then((analysis) => setState({ analysis, analyzedSource: source }))
        .catch((reason: unknown) => {
          if (controller.signal.aborted) {
            return
          }

          setState({
            analysis: emptyAnalysis,
            error: reason instanceof Error ? reason.message : 'Message analysis is unavailable.',
          })
        })
    }, 200)

    return () => {
      window.clearTimeout(timeout)
      controller.abort()
    }
  }, [enabled, source])

  return state
}
