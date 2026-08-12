import { useMemo } from 'react'
import { Flex, Input, InputNumber, Space, Typography } from 'antd'
import type { MessageArgument } from '../../entities/message/api/analyzeMessage'
import type { MessagePreviewValue } from '../../entities/message/api/previewMessage'
import { useMessagePreview } from '../../entities/message/model/useMessagePreview'
import { layoutTokens } from '../../design/layoutTokens'

interface MessagePreviewPanelProps {
  source: string
  culture: string
  arguments: readonly MessageArgument[]
  values: Readonly<Record<string, MessagePreviewValue>>
  enabled: boolean
  onValueChange: (name: string, value: MessagePreviewValue) => void
}

export function MessagePreviewPanel({
  source,
  culture,
  arguments: argumentDefinitions,
  values,
  enabled,
  onValueChange,
}: MessagePreviewPanelProps) {
  const previewValues = useMemo(() => Object.fromEntries(
    argumentDefinitions.map((argument) => [
      argument.name,
      normalizePreviewValue(argument, values[argument.name]),
    ]),
  ), [argumentDefinitions, values])
  const state = useMessagePreview(
    source,
    culture,
    previewValues,
    enabled && argumentDefinitions.length > 0,
  )

  if (argumentDefinitions.length === 0) {
    return null
  }

  const previewProblem = state.error ?? state.preview?.diagnostics[0]?.message
  const previewText = state.preview?.isSuccess ? state.preview.text : undefined

  return (
    <Flex
      align="stretch"
      gap={layoutTokens.spacing.large}
      style={{
        flex: 'none',
        marginTop: layoutTokens.spacing.small,
      }}
    >
      <Flex
        vertical
        gap={layoutTokens.spacing.xSmall}
        style={{ flex: '0 0 240px', minWidth: 0, maxHeight: 144, overflow: 'auto' }}
      >
        <Typography.Text type="secondary">Arguments</Typography.Text>
        {argumentDefinitions.map((argument) => {
          const value = previewValues[argument.name]
          return (
            <Space.Compact key={argument.name} block size="small">
              <Space.Addon>{argument.name}</Space.Addon>
              {argument.kind === 'number'
                ? (
                    <InputNumber
                      aria-label={`${argument.name} preview value`}
                      value={typeof value === 'number' ? value : 1}
                      style={{ flex: 1, minWidth: 0 }}
                      onChange={(nextValue) => onValueChange(argument.name, nextValue ?? 0)}
                    />
                  )
                : (
                    <Input
                      aria-label={`${argument.name} preview value`}
                      value={typeof value === 'string' ? value : String(value ?? '')}
                      style={{ minWidth: 0 }}
                      onChange={(event) => onValueChange(argument.name, event.target.value)}
                    />
                  )}
            </Space.Compact>
          )
        })}
      </Flex>
      <Flex vertical gap={layoutTokens.spacing.xSmall} style={{ flex: 1, minWidth: 0 }}>
        <Typography.Text type="secondary">Preview</Typography.Text>
        <Typography.Text
          type={previewProblem ? 'danger' : previewText === undefined ? 'secondary' : undefined}
          aria-live="polite"
          style={{
            maxHeight: 120,
            overflow: 'auto',
            whiteSpace: 'pre-wrap',
            overflowWrap: 'anywhere',
          }}
        >
          {previewProblem ?? previewText ?? 'Formatting preview…'}
        </Typography.Text>
      </Flex>
    </Flex>
  )
}

function normalizePreviewValue(
  argument: MessageArgument,
  value: MessagePreviewValue | undefined,
): MessagePreviewValue {
  if (argument.kind === 'number') {
    return typeof value === 'number' ? value : 1
  }

  return value === undefined ? '' : value
}
