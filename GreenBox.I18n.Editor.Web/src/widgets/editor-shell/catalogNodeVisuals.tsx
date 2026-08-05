import type { ReactNode } from 'react'
import {
  FileTextOutlined,
  FolderOutlined,
  GlobalOutlined,
  TranslationOutlined,
} from '@ant-design/icons'
import type { theme } from 'antd'
import type { CatalogTreeNode } from './catalogTree'

type CatalogThemeToken = ReturnType<typeof theme.useToken>['token']

export function getCatalogNodeIconColor(kind: CatalogTreeNode['kind'], token: CatalogThemeToken) {
  switch (kind) {
    case 'locales-root':
    case 'locale':
      return token.colorSuccess
    case 'entries-root':
      return token.colorError
    case 'folder':
    case 'folder-draft':
      return token.colorWarning
    case 'entry':
    case 'entry-draft':
      return token.colorInfo
  }
}

export function renderCatalogNodeIcon(kind: CatalogTreeNode['kind'], color: string): ReactNode {
  const style = { color }

  switch (kind) {
    case 'locales-root':
      return <GlobalOutlined style={style} />
    case 'entries-root':
      return <TranslationOutlined style={style} />
    case 'locale':
      return <GlobalOutlined style={style} />
    case 'folder':
    case 'folder-draft':
      return <FolderOutlined style={style} />
    case 'entry':
    case 'entry-draft':
      return <FileTextOutlined style={style} />
  }
}
