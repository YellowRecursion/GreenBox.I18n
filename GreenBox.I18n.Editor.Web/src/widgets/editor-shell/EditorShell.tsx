import { useCallback, useEffect, useMemo, useRef, useState, type Key, type ReactNode } from 'react'
import { EditOutlined, ProjectOutlined, SettingOutlined, UserOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Empty, Flex, Spin, Splitter, Switch, Tabs, Tag, Tooltip, Typography, theme } from 'antd'
import { layoutTokens } from '../../design/layoutTokens'
import type { CatalogAssetReference, CatalogLocale, CatalogSnapshot } from '../../entities/catalog/model/catalog'
import type { CatalogEntryMove } from '../../entities/catalog/api/moveCatalogEntries'
import type { CatalogEntryDelta } from '../../entities/catalog/api/applyCatalogEntryDelta'
import type { CatalogLocaleRename } from '../../entities/catalog/api/applyCatalogLocales'
import { useCatalog } from '../../entities/catalog/model/useCatalog'
import { useCatalogSession } from '../../entities/catalog/model/useCatalogSession'
import { useCatalogSourceMonitor } from '../../entities/catalog/model/useCatalogSourceMonitor'
import {
  getEditorPreferences,
  updateEditorPreferences,
  type EditorPreferences,
} from '../../entities/preferences/api/editorPreferences'
import { OpenCatalogDialog } from '../../features/open-catalog/OpenCatalogDialog'
import { CatalogInspector } from './CatalogInspector'
import { CatalogTreePanel } from './CatalogTreePanel'
import { EditorHeader } from './EditorHeader'
import { buildCatalogTree, type CatalogSelectionItem } from './catalogTree'
import {
  createEditorHistoryEntry,
  emptyEditorHistory,
  type EditorHistoryEntry,
  type EditorHistoryStateSnapshot,
} from './editorHistory'

const historyLimit = 100
type EditorPage = 'editor' | 'settings'

export function EditorShell() {
  const { token } = theme.useToken()
  const { state: session } = useCatalogSession()
  const {
    state: catalog,
    addEntry,
    removeEntries,
    moveEntries,
    applyEntryDelta,
    applyLocales,
    save,
    revert,
    mergeSource,
  } = useCatalog()

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
      onApplyEntryDelta={applyEntryDelta}
      onApplyLocales={applyLocales}
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
  onApplyEntryDelta,
  onApplyLocales,
  onSave,
  onRevert,
  onMergeSource,
}: {
  catalog: CatalogSnapshot
  catalogPath: string
  onAddEntry(path: string): Promise<CatalogSnapshot>
  onRemoveEntries(ids: string[]): Promise<CatalogSnapshot>
  onMoveEntries(moves: CatalogEntryMove[]): Promise<CatalogSnapshot>
  onApplyEntryDelta(delta: CatalogEntryDelta, expectedRevision: number): Promise<CatalogSnapshot>
  onApplyLocales(
    locales: CatalogLocale[],
    defaultLocale: string,
    expectedRevision: number,
    renames?: CatalogLocaleRename[],
    removedIds?: string[],
  ): Promise<CatalogSnapshot>
  onSave(overwriteExternalChanges?: boolean): Promise<CatalogSnapshot>
  onRevert(): Promise<CatalogSnapshot>
  onMergeSource(): Promise<CatalogSnapshot>
}) {
  const { token } = theme.useToken()
  const [activePage, setActivePage] = useState<EditorPage>('editor')
  const [selectedKeys, setSelectedKeys] = useState<Key[]>([])
  const [expandedKeys, setExpandedKeys] = useState<Key[]>(['root:entries'])
  const [temporaryFolderPaths, setTemporaryFolderPaths] = useState<string[]>([])
  const [history, setHistory] = useState(emptyEditorHistory)
  const [historyDirection, setHistoryDirection] = useState<'undo' | 'redo'>()
  const historyBusyRef = useRef(false)
  const selectionHistoryRef = useRef<Key[][]>([[]])
  const selectionHistoryIndexRef = useRef(0)
  const isNavigatingSelectionRef = useRef(false)
  const tree = useMemo(
    () => buildCatalogTree(catalog, temporaryFolderPaths),
    [catalog, temporaryFolderPaths],
  )
  const selection = selectedKeys.flatMap((key) => {
    const item = tree.selectionByKey.get(String(key))
    return item ? [item] : []
  })

  useEffect(() => {
    if (isNavigatingSelectionRef.current) {
      isNavigatingSelectionRef.current = false
      return
    }

    const selectionHistory = selectionHistoryRef.current
    const selectionHistoryIndex = selectionHistoryIndexRef.current
    if (areKeySelectionsEqual(selectionHistory[selectionHistoryIndex], selectedKeys)) {
      return
    }

    const nextHistory = [
      ...selectionHistory.slice(0, selectionHistoryIndex + 1),
      selectedKeys,
    ].slice(-historyLimit)
    selectionHistoryRef.current = nextHistory
    selectionHistoryIndexRef.current = nextHistory.length - 1
  }, [selectedKeys])

  useEffect(() => {
    const navigateSelection = (direction: -1 | 1) => {
      const selectionHistory = selectionHistoryRef.current
      let targetIndex = selectionHistoryIndexRef.current + direction

      while (targetIndex >= 0 && targetIndex < selectionHistory.length) {
        const targetSelection = selectionHistory[targetIndex]
        const existingSelection = targetSelection.filter((key) =>
          tree.selectionByKey.has(String(key)))

        if (targetSelection.length === 0 || existingSelection.length > 0) {
          selectionHistoryIndexRef.current = targetIndex
          isNavigatingSelectionRef.current = true
          setSelectedKeys(existingSelection)
          return
        }

        targetIndex += direction
      }
    }

    const handleMouseDown = (event: MouseEvent) => {
      if (event.button !== 3 && event.button !== 4) {
        return
      }

      event.preventDefault()
      event.stopPropagation()
      navigateSelection(event.button === 3 ? -1 : 1)
    }

    const preventMouseNavigation = (event: MouseEvent) => {
      if (event.button === 3 || event.button === 4) {
        event.preventDefault()
        event.stopPropagation()
      }
    }

    window.addEventListener('mousedown', handleMouseDown, true)
    window.addEventListener('auxclick', preventMouseNavigation, true)
    return () => {
      window.removeEventListener('mousedown', handleMouseDown, true)
      window.removeEventListener('auxclick', preventMouseNavigation, true)
    }
  }, [tree.selectionByKey])

  const clearHistory = useCallback(() => {
    setHistory(emptyEditorHistory)
  }, [])

  const recordHistory = useCallback((entry: EditorHistoryEntry | undefined) => {
    if (!entry) {
      return
    }

    setHistory((current) => ({
      undo: [...current.undo, entry].slice(-historyLimit),
      redo: [],
    }))
  }, [])

  const applyHistorySnapshot = useCallback(async (snapshot: EditorHistoryStateSnapshot) => {
    let expectedRevision = catalog.revision
    if (snapshot.localeState) {
      const localeRenames = inferLocaleRenames(catalog.locales, snapshot.localeState.locales)
      const targetLocaleIds = new Set(snapshot.localeState.locales.map((locale) => locale.id))
      const removedLocaleIds = catalog.locales
        .map((locale) => locale.id)
        .filter((id) => !targetLocaleIds.has(id) &&
          !localeRenames.some((rename) => rename.fromId === id))
      const updatedCatalog = await onApplyLocales(
        snapshot.localeState.locales,
        snapshot.localeState.defaultLocale,
        expectedRevision,
        localeRenames,
        removedLocaleIds,
      )
      expectedRevision = updatedCatalog.revision
      if (localeRenames.length > 0) {
        setSelectedKeys((keys) => keys.map((key) => {
          const rename = localeRenames.find((candidate) =>
            String(key) === `locale:${candidate.fromId}`)
          return rename ? `locale:${rename.toId}` : key
        }))
      }
    }

    if (snapshot.entryDelta.entries.length > 0 || snapshot.entryDelta.removedIds.length > 0) {
      await onApplyEntryDelta(snapshot.entryDelta, expectedRevision)
    }

    setTemporaryFolderPaths(snapshot.temporaryFolderPaths)
    const selectedEntryIds = new Set(selectedKeys
      .map(String)
      .filter((key) => key.startsWith('entry:'))
      .map((key) => key.slice('entry:'.length)))
    const selectedEntryAncestorKeys = snapshot.entryDelta.entries
      .filter((entry) => selectedEntryIds.has(entry.id))
      .flatMap((entry) => getEntryAncestorFolderKeys(entry.path))
    setExpandedKeys((expanded) => mergeKeys(expanded, selectedEntryAncestorKeys))
  }, [catalog.revision, onApplyEntryDelta, onApplyLocales, selectedKeys])

  const applyHistoryEntry = useCallback(async (
    direction: 'undo' | 'redo',
    entry: EditorHistoryEntry,
  ) => {
    if (historyBusyRef.current) {
      return
    }

    historyBusyRef.current = true
    setHistoryDirection(direction)
    try {
      await applyHistorySnapshot(direction === 'undo' ? entry.before : entry.after)
      setHistory((current) => direction === 'undo'
        ? {
            undo: current.undo.slice(0, -1),
            redo: [...current.redo, entry].slice(-historyLimit),
          }
        : {
            undo: [...current.undo, entry].slice(-historyLimit),
            redo: current.redo.slice(0, -1),
          })
    } finally {
      historyBusyRef.current = false
      setHistoryDirection(undefined)
    }
  }, [applyHistorySnapshot])

  const handleUndo = useCallback(async () => {
    const entry = history.undo.at(-1)
    if (entry) {
      await applyHistoryEntry('undo', entry)
    }
  }, [applyHistoryEntry, history.undo])

  const handleRedo = useCallback(async () => {
    const entry = history.redo.at(-1)
    if (entry) {
      await applyHistoryEntry('redo', entry)
    }
  }, [applyHistoryEntry, history.redo])

  const handleAddEntry = async (path: string) => {
    const beforeTemporaryFolderPaths = temporaryFolderPaths
    const updatedCatalog = await onAddEntry(path)
    const entry = updatedCatalog.entries.find((candidate) => candidate.path === path)
    if (!entry) {
      throw new Error(`Created entry '${path}' was not returned by the editor host.`)
    }

    const key = `entry:${entry.id}`
    const parentPath = path.includes('.') ? path.slice(0, path.lastIndexOf('.')) : ''
    const afterTemporaryFolderPaths = temporaryFolderPaths.filter((temporaryPath) =>
      parentPath !== temporaryPath && !parentPath.startsWith(`${temporaryPath}.`))
    setTemporaryFolderPaths(afterTemporaryFolderPaths)
    recordHistory(createEditorHistoryEntry(
      `Add ${path}`,
      catalog,
      updatedCatalog,
      beforeTemporaryFolderPaths,
      afterTemporaryFolderPaths,
    ))
    return key
  }

  const handleAddFolder = (path: string) => {
    const duplicate = [...tree.selectionByKey.values()].some((item) =>
      item.kind === 'folder' && item.path.toLocaleLowerCase() === path.toLocaleLowerCase())
    if (duplicate) {
      throw new Error(`Folder '${path}' already exists.`)
    }

    const key = `folder:${path}`
    const afterTemporaryFolderPaths = [...temporaryFolderPaths, path]
    setTemporaryFolderPaths(afterTemporaryFolderPaths)
    recordHistory(createEditorHistoryEntry(
      `Add folder ${path}`,
      catalog,
      catalog,
      temporaryFolderPaths,
      afterTemporaryFolderPaths,
    ))
    return key
  }

  const handleRemoveNodes = async (keys: Key[]) => {
    const beforeTemporaryFolderPaths = temporaryFolderPaths
    const items = keys.flatMap((key) => {
      const item = tree.selectionByKey.get(String(key))
      return item ? [item] : []
    })
    const folderPaths = items
      .filter((item) => item.kind === 'folder')
      .map((item) => item.path)
    const localeIds = new Set(
      items.filter((item) => item.kind === 'locale').map((item) => item.locale.id),
    )
    const entryIds = new Set(
      items.filter((item) => item.kind === 'entry').map((item) => item.entry.id),
    )

    for (const entry of catalog.entries) {
      if (folderPaths.some((path) => entry.path.startsWith(`${path}.`))) {
        entryIds.add(entry.id)
      }
    }

    const remainingLocales = catalog.locales.filter((locale) => !localeIds.has(locale.id))
    if (localeIds.size > 0 && remainingLocales.length === 0) {
      throw new Error('The catalog must contain at least one locale.')
    }

    let updatedCatalog = catalog
    if (localeIds.size > 0) {
      const nextDefaultLocale = localeIds.has(catalog.defaultLocale)
        ? remainingLocales[0].id
        : catalog.defaultLocale
      updatedCatalog = await onApplyLocales(
        remainingLocales.map((locale) => locale.fallback && localeIds.has(locale.fallback)
          ? { ...locale, fallback: null }
          : locale),
        nextDefaultLocale,
        updatedCatalog.revision,
        [],
        [...localeIds],
      )
    }

    if (entryIds.size > 0) {
      updatedCatalog = await onRemoveEntries([...entryIds])
    }

    const possibleEmptySourceFolders = catalog.entries
      .filter((entry) => entryIds.has(entry.id))
      .filter((entry) => !folderPaths.some((path) => entry.path.startsWith(`${path}.`)))
      .map((entry) => getParentPath(entry.path))
      .filter((path) => Boolean(path))
    const emptySourceFolders = possibleEmptySourceFolders.filter((folderPath) =>
      !updatedCatalog.entries.some((entry) => entry.path.startsWith(`${folderPath}.`)))
    const afterTemporaryFolderPaths = [...new Set([
      ...temporaryFolderPaths.filter((temporaryPath) =>
        !folderPaths.some((path) =>
          temporaryPath === path || temporaryPath.startsWith(`${path}.`))),
      ...emptySourceFolders,
    ])]
    setTemporaryFolderPaths(afterTemporaryFolderPaths)
    setSelectedKeys((selected) => selected.filter((key) => {
      const value = String(key)
      if (value.startsWith('entry:') && entryIds.has(value.slice('entry:'.length))) {
        return false
      }

      if (value.startsWith('locale:') && localeIds.has(value.slice('locale:'.length))) {
        return false
      }

      return !folderPaths.some((path) =>
        value === `folder:${path}` || value.startsWith(`folder:${path}.`))
    }))
    recordHistory(createEditorHistoryEntry(
      describeRemoveOperation(items, entryIds.size),
      catalog,
      updatedCatalog,
      beforeTemporaryFolderPaths,
      afterTemporaryFolderPaths,
    ))
  }

  const handleMoveNodes = async (keys: Key[], targetKey: Key) => {
    const beforeTemporaryFolderPaths = temporaryFolderPaths
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

    const updatedCatalog = movesById.size > 0
      ? await onMoveEntries([...movesById.values()])
      : catalog

    const afterTemporaryFolderPaths = [...new Set([
      ...temporaryFolderPaths.map((path) => replaceMovedFolderPrefix(path, folderDestinations)),
      ...emptySourceFolders,
    ])]
    setTemporaryFolderPaths(afterTemporaryFolderPaths)
    setSelectedKeys((selected) => selected.map((key) =>
      replaceMovedFolderKey(key, folderDestinations)))
    setExpandedKeys((expanded) => mergeKeys(
      expanded.map((key) => replaceMovedFolderKey(key, folderDestinations)),
      [...folderDestinations.values()].map((path) => `folder:${path}`),
    ))
    recordHistory(createEditorHistoryEntry(
      describeMoveOperation(selectedItems, targetPath),
      catalog,
      updatedCatalog,
      beforeTemporaryFolderPaths,
      afterTemporaryFolderPaths,
    ))
  }

  const handleFolderPathChange = async (sourcePath: string, destinationPath: string) => {
    if (sourcePath === destinationPath) {
      return
    }

    if (destinationPath.startsWith(`${sourcePath}.`)) {
      throw new Error(`Folder '${sourcePath}' cannot be moved into itself.`)
    }

    const duplicateFolder = [...tree.selectionByKey.values()].some((candidate) =>
      candidate.kind === 'folder' &&
      candidate.path !== sourcePath &&
      candidate.path.toLocaleLowerCase() === destinationPath.toLocaleLowerCase())
    if (duplicateFolder) {
      throw new Error(`Folder '${destinationPath}' already exists.`)
    }

    const moves = catalog.entries
      .filter((entry) => entry.path.startsWith(`${sourcePath}.`))
      .map((entry) => ({
        id: entry.id,
        path: `${destinationPath}${entry.path.slice(sourcePath.length)}`,
      }))
    const updatedCatalog = moves.length > 0
      ? await onMoveEntries(moves)
      : catalog

    const destinations = new Map([[sourcePath, destinationPath]])
    const sourceParentPath = getParentPath(sourcePath)
    const sourceParentIsEmpty = sourceParentPath &&
      !updatedCatalog.entries.some((entry) => entry.path.startsWith(`${sourceParentPath}.`))
    const afterTemporaryFolderPaths = [...new Set([
      ...temporaryFolderPaths.map((path) => replaceMovedFolderPrefix(path, destinations)),
      ...(sourceParentIsEmpty ? [sourceParentPath] : []),
    ])]
    setTemporaryFolderPaths(afterTemporaryFolderPaths)
    setSelectedKeys((selected) => selected.map((key) =>
      replaceMovedFolderKey(key, destinations)))
    setExpandedKeys((expanded) => mergeKeys(
      expanded.map((key) => replaceMovedFolderKey(key, destinations)),
      getFolderAncestorKeys(destinationPath),
    ))
    recordHistory(createEditorHistoryEntry(
      `Move folder ${sourcePath} to ${destinationPath}`,
      catalog,
      updatedCatalog,
      temporaryFolderPaths,
      afterTemporaryFolderPaths,
    ))
  }

  const handleRenameNode = async (key: Key, name: string) => {
    const beforeTemporaryFolderPaths = temporaryFolderPaths
    const item = tree.selectionByKey.get(String(key))
    if (!item || item.kind === 'locale') {
      throw new Error('Only entries and folders can be renamed.')
    }

    if (item.kind === 'entry') {
      const path = joinPath(getParentPath(item.entry.path), name)
      const updatedCatalog = await onMoveEntries([{ id: item.entry.id, path }])
      recordHistory(createEditorHistoryEntry(
        `Rename ${item.entry.path} to ${path}`,
        catalog,
        updatedCatalog,
        beforeTemporaryFolderPaths,
        beforeTemporaryFolderPaths,
      ))
      return `entry:${item.entry.id}`
    }

    const destinationPath = joinPath(getParentPath(item.path), name)
    await handleFolderPathChange(item.path, destinationPath)
    return `folder:${destinationPath}`
  }

  const handleEntryPathChange = async (id: string, path: string) => {
    const entry = catalog.entries.find((candidate) => candidate.id === id)
    if (!entry) {
      throw new Error(`Entry '${id}' does not exist.`)
    }

    const beforeTemporaryFolderPaths = temporaryFolderPaths
    const updatedCatalog = await onMoveEntries([{ id, path }])
    setExpandedKeys((expanded) => mergeKeys(expanded, getEntryAncestorFolderKeys(path)))
    const sourceFolderPath = getParentPath(entry.path)
    const sourceFolderIsEmpty = sourceFolderPath &&
      !updatedCatalog.entries.some((candidate) => candidate.path.startsWith(`${sourceFolderPath}.`))
    const afterTemporaryFolderPaths = sourceFolderIsEmpty
      ? [...new Set([...temporaryFolderPaths, sourceFolderPath])]
      : temporaryFolderPaths
    setTemporaryFolderPaths(afterTemporaryFolderPaths)
    recordHistory(createEditorHistoryEntry(
      `Move ${entry.path} to ${path}`,
      catalog,
      updatedCatalog,
      beforeTemporaryFolderPaths,
      afterTemporaryFolderPaths,
    ))
  }

  const handleEntryCommentChange = async (id: string, comment: string | null) => {
    const entry = catalog.entries.find((candidate) => candidate.id === id)
    if (!entry) {
      throw new Error(`Entry '${id}' does not exist.`)
    }

    const updatedCatalog = await onApplyEntryDelta({
      entries: [{ ...entry, comment }],
      removedIds: [],
    }, catalog.revision)
    recordHistory(createEditorHistoryEntry(
      `Edit comment for ${entry.path}`,
      catalog,
      updatedCatalog,
      temporaryFolderPaths,
      temporaryFolderPaths,
    ))
  }

  const handleEntryAssetChange = async (
    id: string,
    localeId: string,
    asset: CatalogAssetReference | null,
  ) => {
    const entry = catalog.entries.find((candidate) => candidate.id === id)
    if (!entry) {
      throw new Error(`Entry '${id}' does not exist.`)
    }

    const localeValue = entry.locales[localeId] ?? { text: null, asset: null }
    const updatedEntry = {
      ...entry,
      locales: {
        ...entry.locales,
        [localeId]: { ...localeValue, asset },
      },
    }
    const updatedCatalog = await onApplyEntryDelta({
      entries: [updatedEntry],
      removedIds: [],
    }, catalog.revision)
    recordHistory(createEditorHistoryEntry(
      `${asset ? 'Assign' : 'Remove'} asset for ${entry.path} (${localeId})`,
      catalog,
      updatedCatalog,
      temporaryFolderPaths,
      temporaryFolderPaths,
    ))
  }

  const handleEntryTextChange = async (
    id: string,
    localeId: string,
    text: string | null,
  ) => {
    const entry = catalog.entries.find((candidate) => candidate.id === id)
    if (!entry) {
      throw new Error(`Entry '${id}' does not exist.`)
    }

    const localeValue = entry.locales[localeId] ?? { text: null, asset: null }
    const updatedEntry = {
      ...entry,
      locales: {
        ...entry.locales,
        [localeId]: { ...localeValue, text },
      },
    }
    const updatedCatalog = await onApplyEntryDelta({
      entries: [updatedEntry],
      removedIds: [],
    }, catalog.revision)
    recordHistory(createEditorHistoryEntry(
      `Edit text for ${entry.path} (${localeId})`,
      catalog,
      updatedCatalog,
      temporaryFolderPaths,
      temporaryFolderPaths,
    ))
  }

  const handleLocaleChange = async (locale: CatalogLocale, previousId = locale.id) => {
    const current = catalog.locales.find((candidate) => candidate.id === previousId)
    if (!current) {
      throw new Error(`Locale '${previousId}' does not exist.`)
    }

    const isRename = previousId !== locale.id
    const updatedLocales = catalog.locales.map((candidate) => {
      if (candidate.id === previousId) {
        return locale
      }

      return isRename && candidate.fallback === previousId
        ? { ...candidate, fallback: locale.id }
        : candidate
    })
    const updatedCatalog = await onApplyLocales(
      updatedLocales,
      isRename && catalog.defaultLocale === previousId ? locale.id : catalog.defaultLocale,
      catalog.revision,
      isRename ? [{ fromId: previousId, toId: locale.id }] : [],
    )
    if (isRename) {
      setSelectedKeys((keys) => keys.map((key) =>
        String(key) === `locale:${previousId}` ? `locale:${locale.id}` : key))
    }
    recordHistory(createEditorHistoryEntry(
      isRename ? `Rename locale ${previousId} to ${locale.id}` : `Edit locale ${locale.id}`,
      catalog,
      updatedCatalog,
      temporaryFolderPaths,
      temporaryFolderPaths,
    ))
  }

  const handleAddLocale = async (locale: CatalogLocale) => {
    const updatedCatalog = await onApplyLocales(
      [...catalog.locales, locale],
      catalog.defaultLocale,
      catalog.revision,
    )
    recordHistory(createEditorHistoryEntry(
      `Add locale ${locale.id}`,
      catalog,
      updatedCatalog,
      temporaryFolderPaths,
      temporaryFolderPaths,
    ))
    setExpandedKeys((keys) => mergeKeys(keys, ['root:locales']))
    return `locale:${locale.id}`
  }

  const handleDefaultLocaleChange = async (localeId: string) => {
    if (localeId === catalog.defaultLocale) {
      return
    }

    const updatedLocales = catalog.locales.map((locale) =>
      locale.id === localeId ? { ...locale, fallback: null } : locale)
    const updatedCatalog = await onApplyLocales(
      updatedLocales,
      localeId,
      catalog.revision,
    )
    recordHistory(createEditorHistoryEntry(
      `Set ${localeId} as default locale`,
      catalog,
      updatedCatalog,
      temporaryFolderPaths,
      temporaryFolderPaths,
    ))
  }

  const reloadFromDisk = useCallback(async () => {
    await onRevert()
    setTemporaryFolderPaths([])
    clearHistory()
  }, [clearHistory, onRevert])

  const isDirty =
    (catalog.hasChanges ??
      (catalog.dirtyEntryIds.length > 0 || (catalog.dirtyPaths?.length ?? 0) > 0)) ||
    temporaryFolderPaths.length > 0

  const mergeFromDisk = useCallback(async () => {
    await onMergeSource()
    clearHistory()
  }, [clearHistory, onMergeSource])

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
        undoLabel={history.undo.at(-1)?.label}
        redoLabel={history.redo.at(-1)?.label}
        historyDirection={historyDirection}
        onUndo={handleUndo}
        onRedo={handleRedo}
        onSave={handleSave}
        onRevert={handleRevert}
      />

      <Flex style={{ flex: 1, minWidth: 0, minHeight: 0 }}>
        <EditorNavigationRail activePage={activePage} onPageChange={setActivePage} />
        {activePage === 'editor' ? (
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
                onAddLocale={handleAddLocale}
                onAddFolder={handleAddFolder}
                onRemoveNodes={handleRemoveNodes}
                onMoveNodes={handleMoveNodes}
                onRenameNode={handleRenameNode}
              />
            </div>
          </Splitter.Panel>
          <Splitter.Panel min="360">
            <div
              style={{
                height: '100%',
                paddingTop: layoutTokens.spacing.large,
                paddingInline: layoutTokens.spacing.xLarge,
                paddingBottom: layoutTokens.spacing.xLarge,
                overflow: 'auto',
              }}
            >
              <CatalogInspector
                selection={selection}
                defaultLocale={catalog.defaultLocale}
                locales={catalog.locales}
                entries={catalog.entries}
                onFolderPathChange={handleFolderPathChange}
                onLocaleChange={handleLocaleChange}
                onDefaultLocaleChange={handleDefaultLocaleChange}
                onEntryPathChange={handleEntryPathChange}
                onEntryCommentChange={handleEntryCommentChange}
                onEntryTextChange={handleEntryTextChange}
                onEntryAssetChange={handleEntryAssetChange}
              />
            </div>
          </Splitter.Panel>
          </Splitter>
        ) : (
          <SettingsPage />
        )}
      </Flex>
    </Flex>
  )
}

function EditorNavigationRail({
  activePage,
  onPageChange,
}: {
  activePage: EditorPage
  onPageChange: (page: EditorPage) => void
}) {
  const { token } = theme.useToken()

  return (
    <aside
      style={{
        width: 48,
        flex: '0 0 48px',
        background: token.colorBgContainer,
        borderInlineEnd: `1px solid ${token.colorBorderSecondary}`,
        paddingTop: layoutTokens.spacing.small,
      }}
    >
      <Flex vertical align="center" gap={layoutTokens.spacing.small}>
        <Tooltip title="Editor" placement="right">
          <Button
            type={activePage === 'editor' ? 'primary' : 'text'}
            icon={<EditOutlined />}
            aria-label="Editor"
            onClick={() => onPageChange('editor')}
            style={{ width: token.controlHeight, paddingInline: 0 }}
          />
        </Tooltip>
        <Tooltip title="Settings" placement="right">
          <Button
            type={activePage === 'settings' ? 'primary' : 'text'}
            icon={<SettingOutlined />}
            aria-label="Settings"
            onClick={() => onPageChange('settings')}
            style={{ width: token.controlHeight, paddingInline: 0 }}
          />
        </Tooltip>
      </Flex>
    </aside>
  )
}

function SettingsPage() {
  return (
    <div style={{ flex: 1, minWidth: 0, overflow: 'auto' }}>
      <Flex
        vertical
        gap={layoutTokens.spacing.large}
        style={{
          width: '100%',
          maxWidth: 800,
          marginInline: 'auto',
          padding: layoutTokens.spacing.xLarge,
        }}
      >
        <div>
          <Typography.Title level={3} style={{ margin: 0 }}>Settings</Typography.Title>
          <Typography.Text type="secondary">
            Configure the Unity project and your personal editor preferences.
          </Typography.Text>
        </div>

        <Tabs
          defaultActiveKey="project"
          items={[
            {
              key: 'project',
              label: 'Project',
              children: (
                <SettingsScopeCard
                  icon={<ProjectOutlined />}
                  title="Project Settings"
                  tag="Shared"
                  description="Settings shared by everyone working with this Unity project."
                  storage="ProjectSettings/GreenBox.I18n.json"
                  emptyText="No project settings are available yet."
                />
              ),
            },
            {
              key: 'preferences',
              label: 'Preferences',
              children: <PreferencesSettingsCard />,
            },
          ]}
        />
      </Flex>
    </div>
  )
}

function PreferencesSettingsCard() {
  const [preferences, setPreferences] = useState<EditorPreferences>()
  const [error, setError] = useState<string>()
  const [isSaving, setIsSaving] = useState(false)

  useEffect(() => {
    const abortController = new AbortController()
    getEditorPreferences(abortController.signal)
      .then(setPreferences)
      .catch((reason: unknown) => {
        if (!abortController.signal.aborted) {
          setError(reason instanceof Error ? reason.message : 'Preferences could not be loaded.')
        }
      })
    return () => abortController.abort()
  }, [])

  const setReopenLastCatalog = async (value: boolean) => {
    setIsSaving(true)
    setError(undefined)
    try {
      setPreferences(await updateEditorPreferences(value))
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : 'Preferences could not be saved.')
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <Flex vertical gap={layoutTokens.spacing.xLarge}>
      <Flex vertical gap={layoutTokens.spacing.small}>
        <Flex align="center" gap={layoutTokens.spacing.small}>
          <UserOutlined />
          <Typography.Text strong>Preferences</Typography.Text>
          <Tag bordered={false}>Personal</Tag>
        </Flex>
        <Typography.Text type="secondary">
          Personal editor preferences for the current user on this device.
        </Typography.Text>
      </Flex>

      {error && <Alert type="error" showIcon message={error} />}
      {preferences?.restoreError && (
        <Alert
          type="warning"
          showIcon
          message="The last catalog could not be reopened"
          description={preferences.restoreError}
        />
      )}

      <Flex vertical gap={0}>
        <Typography.Text strong type="secondary">Startup</Typography.Text>
        <Card size="small" style={{ marginTop: layoutTokens.spacing.small }}>
          <Flex align="center" justify="space-between" gap={layoutTokens.spacing.xLarge}>
            <Flex vertical gap={layoutTokens.spacing.xSmall} style={{ minWidth: 0 }}>
              <Typography.Text>Reopen last catalog</Typography.Text>
              <Typography.Text type="secondary">
                Automatically open the most recently used catalog when application starts.
              </Typography.Text>
            </Flex>
            <Switch
              checked={preferences?.reopenLastCatalog ?? false}
              loading={!preferences || isSaving}
              disabled={!preferences || isSaving}
              onChange={(value) => void setReopenLastCatalog(value)}
            />
          </Flex>
        </Card>
      </Flex>
    </Flex>
  )
}

function SettingsScopeCard({
  icon,
  title,
  tag,
  description,
  storage,
  emptyText,
}: {
  icon: ReactNode
  title: string
  tag: string
  description: string
  storage?: string
  emptyText: string
}) {
  return (
    <Flex vertical gap={layoutTokens.spacing.xLarge}>
      <Flex vertical gap={layoutTokens.spacing.small}>
        <Flex align="center" gap={layoutTokens.spacing.small}>
          {icon}
          <Typography.Text strong>{title}</Typography.Text>
          <Tag bordered={false}>{tag}</Tag>
        </Flex>
        <Typography.Text type="secondary">{description}</Typography.Text>
        {storage && (
          <Typography.Text type="secondary">
            Storage: <Typography.Text code>{storage}</Typography.Text>
          </Typography.Text>
        )}
      </Flex>
      <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={emptyText} />
    </Flex>
  )
}

function inferLocaleRenames(
  currentLocales: CatalogLocale[],
  targetLocales: CatalogLocale[],
): CatalogLocaleRename[] {
  if (currentLocales.length !== targetLocales.length) {
    return []
  }

  const currentIds = new Set(currentLocales.map((locale) => locale.id))
  const targetIds = new Set(targetLocales.map((locale) => locale.id))
  const removedIds = [...currentIds].filter((id) => !targetIds.has(id))
  const addedIds = [...targetIds].filter((id) => !currentIds.has(id))
  return removedIds.length === 1 && addedIds.length === 1
    ? [{ fromId: removedIds[0], toId: addedIds[0] }]
    : []
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

function describeRemoveOperation(items: CatalogSelectionItem[], removedEntryCount: number) {
  if (items.length === 1) {
    const item = items[0]
    if (item.kind === 'entry') {
      return `Delete ${item.entry.path}`
    }

    if (item.kind === 'folder') {
      return `Delete folder ${item.path}`
    }

    if (item.kind === 'locale') {
      return `Delete locale ${item.locale.id}`
    }
  }

  const affectedCount = removedEntryCount || items.length
  return `Delete ${affectedCount} ${affectedCount === 1 ? 'item' : 'items'}`
}

function describeMoveOperation(items: CatalogSelectionItem[], targetPath: string) {
  const destination = targetPath || 'Entries'
  if (items.length === 1) {
    const item = items[0]
    const source = item.kind === 'entry'
      ? item.entry.path
      : item.kind === 'folder'
        ? item.path
        : item.locale.id
    return `Move ${source} to ${destination}`
  }

  return `Move ${items.length} items to ${destination}`
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

function getEntryAncestorFolderKeys(path: string): Key[] {
  const segments = path.split('.').slice(0, -1)
  return segments.map((_, index) => `folder:${segments.slice(0, index + 1).join('.')}`)
}

function getFolderAncestorKeys(path: string): Key[] {
  const segments = path.split('.')
  return segments.map((_, index) => `folder:${segments.slice(0, index + 1).join('.')}`)
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

function areKeySelectionsEqual(left: Key[] | undefined, right: Key[]) {
  return left?.length === right.length && left.every((key, index) => key === right[index])
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
