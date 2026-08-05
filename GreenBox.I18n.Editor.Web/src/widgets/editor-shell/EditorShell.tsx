import { useCallback, useMemo, useState, type Key, type ReactNode } from 'react'
import { Alert, Flex, Spin, Splitter, Typography, theme } from 'antd'
import { layoutTokens } from '../../design/layoutTokens'
import type { CatalogSnapshot } from '../../entities/catalog/model/catalog'
import type { CatalogEntryMove } from '../../entities/catalog/api/moveCatalogEntries'
import { useCatalog } from '../../entities/catalog/model/useCatalog'
import { useCatalogSession } from '../../entities/catalog/model/useCatalogSession'
import { useCatalogSourceMonitor } from '../../entities/catalog/model/useCatalogSourceMonitor'
import { OpenCatalogDialog } from '../../features/open-catalog/OpenCatalogDialog'
import { CatalogInspector } from './CatalogInspector'
import { CatalogTreePanel } from './CatalogTreePanel'
import { EditorHeader } from './EditorHeader'
import { buildCatalogTree } from './catalogTree'

export function EditorShell() {
  const { token } = theme.useToken()
  const { state: session } = useCatalogSession()
  const { state: catalog, addEntry, removeEntries, moveEntries, save, revert, mergeSource } = useCatalog()

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
      onRemoveEntries={removeEntries}
      onMoveEntries={moveEntries}
      onSave={save}
      onRevert={revert}
      onMergeSource={mergeSource}
    />
  )
}

function CatalogWorkspace({
  catalog,
  catalogPath,
  onAddEntry,
  onRemoveEntries,
  onMoveEntries,
  onSave,
  onRevert,
  onMergeSource,
}: {
  catalog: CatalogSnapshot
  catalogPath: string
  onAddEntry(path: string): Promise<CatalogSnapshot>
  onRemoveEntries(ids: string[]): Promise<CatalogSnapshot>
  onMoveEntries(moves: CatalogEntryMove[]): Promise<CatalogSnapshot>
  onSave(overwriteExternalChanges?: boolean): Promise<CatalogSnapshot>
  onRevert(): Promise<CatalogSnapshot>
  onMergeSource(): Promise<CatalogSnapshot>
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

  const handleRemoveNodes = async (keys: Key[]) => {
    const items = keys.flatMap((key) => {
      const item = tree.selectionByKey.get(String(key))
      return item ? [item] : []
    })
    const folderPaths = items
      .filter((item) => item.kind === 'folder')
      .map((item) => item.path)
    const entryIds = new Set(
      items.filter((item) => item.kind === 'entry').map((item) => item.entry.id),
    )

    for (const entry of catalog.entries) {
      if (folderPaths.some((path) => entry.path.startsWith(`${path}.`))) {
        entryIds.add(entry.id)
      }
    }

    if (entryIds.size > 0) {
      await onRemoveEntries([...entryIds])
    }

    setTemporaryFolderPaths((paths) => paths.filter((temporaryPath) =>
      !folderPaths.some((path) =>
        temporaryPath === path || temporaryPath.startsWith(`${path}.`))))
    setSelectedKeys((selected) => selected.filter((key) => {
      const value = String(key)
      if (value.startsWith('entry:') && entryIds.has(value.slice('entry:'.length))) {
        return false
      }

      return !folderPaths.some((path) =>
        value === `folder:${path}` || value.startsWith(`folder:${path}.`))
    }))
  }

  const handleMoveNodes = async (keys: Key[], targetKey: Key) => {
    const targetPath = getContainerPath(tree, targetKey)
    const selectedItems = keys.flatMap((key) => {
      const item = tree.selectionByKey.get(String(key))
      return item ? [item] : []
    })
    const selectedFolderPaths = selectedItems
      .filter((item) => item.kind === 'folder')
      .map((item) => item.path)
      .filter((path, index, paths) =>
        !paths.some((candidate, candidateIndex) =>
          candidateIndex !== index && path.startsWith(`${candidate}.`)))

    for (const folderPath of selectedFolderPaths) {
      if (targetPath === folderPath || targetPath.startsWith(`${folderPath}.`)) {
        throw new Error(`Folder '${folderPath}' cannot be moved into itself.`)
      }
    }

    const selectedEntryIds = new Set(selectedItems
      .filter((item) => item.kind === 'entry')
      .filter((item) => !selectedFolderPaths.some((path) => item.entry.path.startsWith(`${path}.`)))
      .map((item) => item.entry.id))
    const folderDestinations = new Map(selectedFolderPaths.map((path) => [
      path,
      joinPath(targetPath, getLastSegment(path)),
    ]))
    const movesById = new Map<string, CatalogEntryMove>()
    const possibleEmptySourceFolders = new Set(selectedFolderPaths
      .map(getParentPath)
      .filter((path) => Boolean(path)))

    for (const entry of catalog.entries) {
      const folderPath = selectedFolderPaths.find((path) => entry.path.startsWith(`${path}.`))
      if (folderPath) {
        const destination = folderDestinations.get(folderPath)!
        movesById.set(entry.id, {
          id: entry.id,
          path: `${destination}${entry.path.slice(folderPath.length)}`,
        })
      } else if (selectedEntryIds.has(entry.id)) {
        movesById.set(entry.id, {
          id: entry.id,
          path: joinPath(targetPath, getLastSegment(entry.path)),
        })
        const sourceFolderPath = getParentPath(entry.path)
        if (sourceFolderPath) {
          possibleEmptySourceFolders.add(sourceFolderPath)
        }
      }
    }

    const finalEntryPaths = catalog.entries.map((entry) =>
      movesById.get(entry.id)?.path ?? entry.path)
    const emptySourceFolders = [...possibleEmptySourceFolders].filter((folderPath) =>
      !finalEntryPaths.some((entryPath) => entryPath.startsWith(`${folderPath}.`)))

    if (movesById.size > 0) {
      await onMoveEntries([...movesById.values()])
    }

    setTemporaryFolderPaths((paths) => [...new Set([
      ...paths.map((path) => replaceMovedFolderPrefix(path, folderDestinations)),
      ...emptySourceFolders,
    ])])
    setSelectedKeys((selected) => selected.map((key) =>
      replaceMovedFolderKey(key, folderDestinations)))
    setExpandedKeys((expanded) => mergeKeys(
      expanded.map((key) => replaceMovedFolderKey(key, folderDestinations)),
      [...folderDestinations.values()].map((path) => `folder:${path}`),
    ))
  }

  const handleRenameNode = async (key: Key, name: string) => {
    const item = tree.selectionByKey.get(String(key))
    if (!item || item.kind === 'locale') {
      throw new Error('Only entries and folders can be renamed.')
    }

    if (item.kind === 'entry') {
      const path = joinPath(getParentPath(item.entry.path), name)
      await onMoveEntries([{ id: item.entry.id, path }])
      return `entry:${item.entry.id}`
    }

    const destinationPath = joinPath(getParentPath(item.path), name)
    const duplicateFolder = [...tree.selectionByKey.values()].some((candidate) =>
      candidate.kind === 'folder' &&
      candidate.path !== item.path &&
      candidate.path.toLocaleLowerCase() === destinationPath.toLocaleLowerCase())
    if (duplicateFolder) {
      throw new Error(`Folder '${destinationPath}' already exists.`)
    }

    const moves = catalog.entries
      .filter((entry) => entry.path.startsWith(`${item.path}.`))
      .map((entry) => ({
        id: entry.id,
        path: `${destinationPath}${entry.path.slice(item.path.length)}`,
      }))
    if (moves.length > 0) {
      await onMoveEntries(moves)
    }

    const destinations = new Map([[item.path, destinationPath]])
    setTemporaryFolderPaths((paths) => paths.map((path) =>
      replaceMovedFolderPrefix(path, destinations)))
    setSelectedKeys((selected) => selected.map((selectedKey) =>
      replaceMovedFolderKey(selectedKey, destinations)))
    setExpandedKeys((expanded) => expanded.map((expandedKey) =>
      replaceMovedFolderKey(expandedKey, destinations)))
    return `folder:${destinationPath}`
  }

  const reloadFromDisk = useCallback(async () => {
    await onRevert()
    setTemporaryFolderPaths([])
  }, [onRevert])

  const isDirty =
    (catalog.hasChanges ??
      (catalog.dirtyEntryIds.length > 0 || (catalog.dirtyPaths?.length ?? 0) > 0)) ||
    temporaryFolderPaths.length > 0

  const mergeFromDisk = useCallback(async () => {
    await onMergeSource()
  }, [onMergeSource])

  const sourceMonitor = useCatalogSourceMonitor(mergeFromDisk)

  const handleSave = async (overwriteExternalChanges = false) => {
    await onSave(overwriteExternalChanges)
    setTemporaryFolderPaths([])
    sourceMonitor.markCurrent()
  }

  const handleRevert = async () => {
    await reloadFromDisk()
    sourceMonitor.markCurrent()
  }

  return (
    <Flex vertical style={{ height: '100vh', minHeight: 0, background: token.colorBgBase }}>
      <EditorHeader
        catalogPath={catalogPath}
        isDirty={isDirty}
        sourceStatus={sourceMonitor.status}
        onSave={handleSave}
        onRevert={handleRevert}
      />

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
              onRemoveNodes={handleRemoveNodes}
              onMoveNodes={handleMoveNodes}
              onRenameNode={handleRenameNode}
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

function getContainerPath(tree: ReturnType<typeof buildCatalogTree>, key: Key) {
  if (key === 'root:entries') {
    return ''
  }

  const item = tree.selectionByKey.get(String(key))
  if (item?.kind !== 'folder') {
    throw new Error('Entries can only be moved into a folder or the Entries root.')
  }

  return item.path
}

function joinPath(parent: string, name: string) {
  return parent ? `${parent}.${name}` : name
}

function getLastSegment(path: string) {
  return path.slice(path.lastIndexOf('.') + 1)
}

function getParentPath(path: string) {
  const separatorIndex = path.lastIndexOf('.')
  return separatorIndex < 0 ? '' : path.slice(0, separatorIndex)
}

function replaceMovedFolderPrefix(path: string, destinations: ReadonlyMap<string, string>) {
  for (const [source, destination] of destinations) {
    if (path === source || path.startsWith(`${source}.`)) {
      return `${destination}${path.slice(source.length)}`
    }
  }

  return path
}

function replaceMovedFolderKey(key: Key, destinations: ReadonlyMap<string, string>): Key {
  const value = String(key)
  if (!value.startsWith('folder:')) {
    return key
  }

  return `folder:${replaceMovedFolderPrefix(value.slice('folder:'.length), destinations)}`
}

function mergeKeys(current: Key[], added: Key[]): Key[] {
  return [...new Set([...current, ...added])]
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
