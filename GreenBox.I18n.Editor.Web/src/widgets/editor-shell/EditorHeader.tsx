import { TranslationOutlined } from '@ant-design/icons'
import { Flex, Typography, theme } from 'antd'
import { layoutTokens } from '../../design/layoutTokens'
import { OpenCatalogDialog } from '../../features/open-catalog/OpenCatalogDialog'

interface EditorHeaderProps {
  catalogPath: string
}

export function EditorHeader({ catalogPath }: EditorHeaderProps) {
  const { token } = theme.useToken()

  return (
    <Flex
      align="center"
      gap={layoutTokens.spacing.large}
      style={{
        height: 52,
        flex: '0 0 52px',
        padding: `0 ${layoutTokens.spacing.large}px`,
        borderBottom: `1px solid ${token.colorBorderSecondary}`,
        background: token.colorBgContainer,
        whiteSpace: 'nowrap',
      }}
    >
      <Flex align="center" gap={layoutTokens.spacing.small}>
        <TranslationOutlined style={{ color: token.colorPrimary, fontSize: 20 }} />
        <Typography.Text strong>GreenBox.I18n</Typography.Text>
      </Flex>

      <div style={{ width: 1, height: 20, background: token.colorBorderSecondary }} />

      <Flex align="center" gap={layoutTokens.spacing.xSmall} style={{ minWidth: 0 }} title={catalogPath}>
        <div style={{ minWidth: 0, whiteSpace: 'nowrap' }}>
          <Typography.Text type="secondary">{truncateMiddle(catalogPath, 64)}</Typography.Text>
        </div>
        <OpenCatalogDialog catalogPath={catalogPath} />
      </Flex>

      <div style={{ flex: 1 }} />
    </Flex>
  )
}

function truncateMiddle(value: string, maxLength: number) {
  if (value.length <= maxLength) {
    return value
  }

  const availableLength = maxLength - 1
  const startLength = Math.ceil(availableLength / 2)
  const endLength = Math.floor(availableLength / 2)
  return `${value.slice(0, startLength)}…${value.slice(-endLength)}`
}
