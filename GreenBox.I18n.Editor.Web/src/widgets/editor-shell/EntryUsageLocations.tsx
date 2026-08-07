import { CodeOutlined, ProductOutlined } from '@ant-design/icons'
import { Button, Empty, Flex, List, Spin, Tooltip, Typography, message, theme } from 'antd'
import { useState } from 'react'
import { layoutTokens } from '../../design/layoutTokens'
import type { AssetUsage, CodeUsage } from '../../entities/usage-index/api/getUsageEntry'
import { openUsage } from '../../entities/usage-index/api/openUsage'
import { useUsageEntry } from '../../entities/usage-index/model/useUsageEntry'

interface EntryUsageLocationsProps {
  entryId: string
  usageRevision?: string
  enabled?: boolean
}

type UsageListItem = {
  key: string
  locationId: string
  kind: 'code' | 'asset'
  label: string
  details: string
}

export function EntryUsageLocations({
  entryId,
  usageRevision,
  enabled = true,
}: EntryUsageLocationsProps) {
  const { token } = theme.useToken()
  const state = useUsageEntry(entryId, usageRevision, enabled)
  const [openingLocationId, setOpeningLocationId] = useState<string>()
  const [messageApi, messageContextHolder] = message.useMessage()

  const handleOpen = async (locationId: string) => {
    setOpeningLocationId(locationId)
    try {
      const result = await openUsage(entryId, locationId)
      if (result.status === 'requiresUserAction') {
        messageApi.warning(result.message ?? 'Open the asset in Unity and try again.')
      } else if (result.status !== 'opened') {
        messageApi.error(result.message ?? 'Unity could not open this usage.')
      }
    } catch (error) {
      messageApi.error(error instanceof Error ? error.message : 'Unity could not open this usage.')
    } finally {
      setOpeningLocationId(undefined)
    }
  }

  if (state.status === 'idle' || state.status === 'loading') {
    return (
      <>
        {messageContextHolder}
        <Flex justify="center" style={{ padding: layoutTokens.spacing.large }}>
          <Spin size="small" />
        </Flex>
      </>
    )
  }

  if (state.status === 'error') {
    return (
      <>
        {messageContextHolder}
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={state.message} />
      </>
    )
  }

  const items: UsageListItem[] = [
    ...state.entry.code.map(createCodeUsageItem),
    ...state.entry.assets.map(createAssetUsageItem),
  ]
  return (
    <>
      {messageContextHolder}
      <List
        size="small"
        dataSource={items}
        locale={{ emptyText: 'No usages' }}
        renderItem={(item) => (
          <List.Item style={{ padding: 0 }}>
            <Tooltip title={item.details}>
              <Button
                block
                type="text"
                aria-label={item.label}
                loading={openingLocationId === item.locationId}
                disabled={openingLocationId !== undefined}
                icon={item.kind === 'code'
                  ? <CodeOutlined style={{ color: token.colorInfo }} />
                  : <ProductOutlined style={{ color: token.colorTextSecondary }} />}
                style={{
                  height: token.controlHeight,
                  justifyContent: 'flex-start',
                  paddingInline: token.paddingXS,
                  minWidth: 0,
                }}
                onClick={() => void handleOpen(item.locationId)}
              >
                <Typography.Text ellipsis style={{ minWidth: 0 }}>
                  {item.label}
                </Typography.Text>
              </Button>
            </Tooltip>
          </List.Item>
        )}
      />
    </>
  )
}

function createCodeUsageItem(usage: CodeUsage): UsageListItem {
  const location = `${usage.filePath}:${usage.line}`
  return {
    key: usage.locationId,
    locationId: usage.locationId,
    kind: 'code',
    label: location,
    details: `${usage.assembly} / ${location}`,
  }
}

function createAssetUsageItem(usage: AssetUsage): UsageListItem {
  const fileName = usage.assetPath.split(/[\\/]/).at(-1) ?? usage.assetPath
  const objectPath = usage.objectPath || usage.componentType
  const propertyPath = usage.propertyPath.replace(/(?:^|\.)_greenBoxI18nEntryId$/, '')
  return {
    key: usage.locationId,
    locationId: usage.locationId,
    kind: 'asset',
    label: `${fileName} / ${objectPath}`,
    details: [usage.assetPath, usage.objectPath, usage.componentType, propertyPath]
      .filter(Boolean)
      .join(' / '),
  }
}
