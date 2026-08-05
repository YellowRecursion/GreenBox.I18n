import type { PropsWithChildren } from 'react'
import { ConfigProvider } from 'antd'
import { CatalogSessionProvider } from '../../entities/catalog/model/CatalogSessionProvider'
import { editorTheme } from '../../design/theme'

export function AppProviders({ children }: PropsWithChildren) {
  return (
    <ConfigProvider theme={editorTheme}>
      <CatalogSessionProvider>{children}</CatalogSessionProvider>
    </ConfigProvider>
  )
}
