import { Alert, Flex, Spin, Typography, theme } from 'antd'
import { layoutTokens } from '../../design/layoutTokens'
import { useCatalogSession } from '../../entities/catalog/model/useCatalogSession'
import { OpenCatalogForm } from '../../features/open-catalog/OpenCatalogForm'

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
      <OpenCatalogForm />
    </Flex>
  )
}

function SessionStatus() {
  const { state } = useCatalogSession()

  if (state.status === 'loading') {
    return <Spin size="small" description="Connecting to editor host..." />
  }

  if (state.status === 'error') {
    return <Alert type="error" showIcon message="Editor host is unavailable" description={state.message} />
  }

  if (state.snapshot.hasCatalog) {
    return (
      <Flex vertical align="center" gap={layoutTokens.spacing.xSmall}>
        <Typography.Text>{state.snapshot.catalogPath}</Typography.Text>
        <Typography.Text type="secondary">
          {state.snapshot.localeCount} locales · {state.snapshot.entryCount} entries · default {state.snapshot.defaultLocale}
        </Typography.Text>
      </Flex>
    )
  }

  return (
    <Typography.Text type="secondary">
      Editor host is connected. No catalog is loaded.
    </Typography.Text>
  )
}
