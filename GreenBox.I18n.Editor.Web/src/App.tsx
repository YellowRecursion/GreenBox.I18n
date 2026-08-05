import { ConfigProvider, Flex, Typography, theme } from 'antd'
import { layoutTokens } from './design/layoutTokens'
import { editorTheme } from './design/theme'

function App() {
  return (
    <ConfigProvider theme={editorTheme}>
      <EditorPlaceholder />
    </ConfigProvider>
  )
}

function EditorPlaceholder() {
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
      <Typography.Text type="secondary">
        Editor shell is running.
      </Typography.Text>
    </Flex>
  )
}

export default App
