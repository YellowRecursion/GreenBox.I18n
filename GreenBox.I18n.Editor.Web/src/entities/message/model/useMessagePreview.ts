import { useEffect, useState } from 'react'
import {
  previewMessage,
  type MessagePreview,
  type MessagePreviewValue,
} from '../api/previewMessage'

export interface MessagePreviewState {
  preview?: MessagePreview
  error?: string
}

export function useMessagePreview(
  source: string,
  culture: string,
  argumentsByName: Readonly<Record<string, MessagePreviewValue>>,
  enabled: boolean,
): MessagePreviewState {
  const [state, setState] = useState<MessagePreviewState>({})

  useEffect(() => {
    if (!enabled) {
      setState({})
      return
    }

    const controller = new AbortController()
    const timeout = window.setTimeout(() => {
      void previewMessage(source, culture, argumentsByName, controller.signal)
        .then((preview) => setState({ preview }))
        .catch((reason: unknown) => {
          if (controller.signal.aborted) {
            return
          }

          setState({
            error: reason instanceof Error ? reason.message : 'Message preview is unavailable.',
          })
        })
    }, 200)

    return () => {
      window.clearTimeout(timeout)
      controller.abort()
    }
  }, [argumentsByName, culture, enabled, source])

  return state
}
