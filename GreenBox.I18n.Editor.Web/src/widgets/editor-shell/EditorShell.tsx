import { Alert, Flex, Spin, Typography, theme } from 'antd'
import { layoutTokens } from '../../design/layoutTokens'
import { useCatalogSession } from '../../entities/catalog/model/useCatalogSession'

export function EditorShell() {
  const { token } = theme.useToken()

  return (
    <Flex
      vertical
      align="center"
      justify="center"
      gap={layoutTokens.spacing.small}
      style={{
        minHeight: '100vh',
        padding: layoutTokens.spacing.xxLarge,
        background: token.colorBgBase,
      }}
    >
      <Typography.Title level={3} style={{ margin: 0 }}>
        GreenBox.I18n Editor
      </Typography.Title>
      <SessionStatus />
    </Flex>
  )
}

function SessionStatus() {
  const session = useCatalogSession()

  if (session.status === 'loading') {
    return <Spin size="small" description="Connecting to editor host..." />
  }

  if (session.status === 'error') {
    return <Alert type="error" showIcon message="Editor host is unavailable" description={session.message} />
  }

  return (
    <Typography.Text type="secondary">
      {session.snapshot.hasCatalog
        ? `Catalog working copy is ready (revision ${session.snapshot.revision}).`
        : 'Editor host is connected. No catalog is loaded.'}
    </Typography.Text>
  )
}
