import { useMemo, useState, type Key, type ReactNode } from 'react'
import { Alert, Flex, Spin, Splitter, Typography, theme } from 'antd'
import { layoutTokens } from '../../design/layoutTokens'
import type { CatalogSnapshot } from '../../entities/catalog/model/catalog'
import { useCatalog } from '../../entities/catalog/model/useCatalog'
import { useCatalogSession } from '../../entities/catalog/model/useCatalogSession'
import { OpenCatalogDialog } from '../../features/open-catalog/OpenCatalogDialog'
import { CatalogInspector } from './CatalogInspector'
import { CatalogTreePanel } from './CatalogTreePanel'
import { EditorHeader } from './EditorHeader'
import { buildCatalogTree } from './catalogTree'

export function EditorShell() {
  const { token } = theme.useToken()
  const { state: session } = useCatalogSession()
  const { state: catalog, addEntry } = useCatalog()

  if (session.status === 'error') {
    return (
      <CenteredShell background={token.colorBgBase}>
        <Alert type="error" showIcon message="Editor host is unavailable" description={session.message} />
      </CenteredShell>
    )
  }

  if (session.status === 'loading') {
    return (
      <CenteredShell background={token.colorBgBase}>
        <Spin description="Connecting to editor host..." />
      </CenteredShell>
    )
  }

  if (!session.snapshot.hasCatalog) {
    return (
      <CenteredShell background={token.colorBgBase}>
        <Typography.Title level={3} style={{ margin: 0 }}>GreenBox.I18n Editor</Typography.Title>
        <Typography.Text type="secondary">Editor host is connected. No catalog is loaded.</Typography.Text>
        <OpenCatalogDialog />
      </CenteredShell>
    )
  }

  if (catalog.status === 'loading' || catalog.status === 'unavailable') {
    return (
      <CenteredShell background={token.colorBgBase}>
        <Spin description="Loading catalog working copy..." />
      </CenteredShell>
    )
  }

  if (catalog.status === 'error') {
    return (
      <CenteredShell background={token.colorBgBase}>
        <Alert type="error" showIcon message="Catalog could not be loaded" description={catalog.message} />
      </CenteredShell>
    )
  }

  return (
    <CatalogWorkspace
      key={session.snapshot.catalogPath ?? ''}
      catalog={catalog.catalog}
      catalogPath={session.snapshot.catalogPath ?? ''}
      onAddEntry={addEntry}
    />
  )
}

function CatalogWorkspace({
  catalog,
  catalogPath,
  onAddEntry,
}: {
  catalog: CatalogSnapshot
  catalogPath: string
  onAddEntry(path: string): Promise<CatalogSnapshot>
}) {
  const { token } = theme.useToken()
  const [selectedKeys, setSelectedKeys] = useState<Key[]>([])
  const [expandedKeys, setExpandedKeys] = useState<Key[]>(['root:locales', 'root:entries'])
  const [temporaryFolderPaths, setTemporaryFolderPaths] = useState<string[]>([])
  const tree = useMemo(
    () => buildCatalogTree(catalog, temporaryFolderPaths),
    [catalog, temporaryFolderPaths],
  )
  const selection = selectedKeys.flatMap((key) => {
    const item = tree.selectionByKey.get(String(key))
    return item ? [item] : []
  })

  const handleAddEntry = async (path: string) => {
    const updatedCatalog = await onAddEntry(path)
    const entry = updatedCatalog.entries.find((candidate) => candidate.path === path)
    if (!entry) {
      throw new Error(`Created entry '${path}' was not returned by the editor host.`)
    }

    const key = `entry:${entry.id}`
    const parentPath = path.includes('.') ? path.slice(0, path.lastIndexOf('.')) : ''
    setTemporaryFolderPaths((paths) => paths.filter((temporaryPath) =>
      parentPath !== temporaryPath && !parentPath.startsWith(`${temporaryPath}.`)))
    return key
  }

  const handleAddFolder = (path: string) => {
    const duplicate = [...tree.selectionByKey.values()].some((item) =>
      item.kind === 'folder' && item.path.toLocaleLowerCase() === path.toLocaleLowerCase())
    if (duplicate) {
      throw new Error(`Folder '${path}' already exists.`)
    }

    const key = `folder:${path}`
    setTemporaryFolderPaths((paths) => [...paths, path])
    return key
  }

  return (
    <Flex vertical style={{ height: '100vh', minHeight: 0, background: token.colorBgBase }}>
      <EditorHeader catalogPath={catalogPath} />

      <Splitter style={{ flex: 1, minHeight: 0 }}>
        <Splitter.Panel defaultSize="34%" min="280" max="60%">
          <div style={{ height: '100%', padding: layoutTokens.spacing.large }}>
            <CatalogTreePanel
              tree={tree}
              selectedKeys={selectedKeys}
              expandedKeys={expandedKeys}
              onSelectionChange={setSelectedKeys}
              onExpandedKeysChange={setExpandedKeys}
              onAddEntry={handleAddEntry}
              onAddFolder={handleAddFolder}
            />
          </div>
        </Splitter.Panel>
        <Splitter.Panel min="360">
          <div style={{ height: '100%', padding: layoutTokens.spacing.xLarge, overflow: 'auto' }}>
            <CatalogInspector
              selection={selection}
              defaultLocale={catalog.defaultLocale}
              locales={catalog.locales}
            />
          </div>
        </Splitter.Panel>
      </Splitter>
    </Flex>
  )
}

function CenteredShell({ background, children }: { background: string; children: ReactNode }) {
  return (
    <Flex
      vertical
      align="center"
      justify="center"
      gap={layoutTokens.spacing.small}
      style={{ minHeight: '100vh', padding: layoutTokens.spacing.xxLarge, background }}
    >
      {children}
    </Flex>
  )
}
