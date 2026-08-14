import {
  memo,
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type ComponentRef,
  type Key,
  type ReactNode,
} from 'react'
import {
  AimOutlined,
  CopyOutlined,
  DeleteOutlined,
  EditOutlined,
  FileAddOutlined,
  FolderAddOutlined,
  PlusOutlined,
  SettingOutlined,
  WarningOutlined,
} from '@ant-design/icons'
import {
  Button,
  AutoComplete,
  Dropdown,
  Flex,
  Input,
  Modal,
  Popover,
  Space,
  Switch,
  Tooltip,
  Tree,
  Typography,
  message,
  theme,
  type InputRef,
  type MenuProps,
  type TreeProps,
} from 'antd'
import { layoutTokens } from '../../design/layoutTokens'
import { LocaleFlag } from '../../entities/catalog/ui/LocaleFlag'
import type { CatalogLocale } from '../../entities/catalog/model/catalog'
import type { EditorPreferences } from '../../entities/preferences/api/editorPreferences'
import {
  filterCatalogTree,
  findMatchingCatalogKeys,
  type CatalogTreeModel,
  type CatalogTreeNode,
} from './catalogTree'
import { getCatalogNodeIconColor, renderCatalogNodeIcon } from './catalogNodeVisuals'
import { commonCultureOptions } from './localeCultures'

interface CatalogTreePanelProps {
  tree: CatalogTreeModel
  selectedKeys: Key[]
  expandedKeys: Key[]
  onSelectionChange: (keys: Key[]) => void
  onExpandedKeysChange: (keys: Key[]) => void
  onAddEntry: (path: string) => Promise<string>
  onAddLocale: (locale: CatalogLocale) => Promise<string>
  onAddFolder: (path: string) => string
  onRemoveNodes: (keys: Key[]) => Promise<void>
  onMoveNodes: (keys: Key[], targetKey: Key) => Promise<void>
  onRenameNode: (key: Key, name: string) => Promise<string>
  warningPreferences: Pick<EditorPreferences, 'warnUnusedEntries' | 'warnIncompleteEntries'>
  usageWarningsAvailable: boolean
  preferencesError?: string
  arePreferencesBusy: boolean
  onWarningPreferencesChange: (
    patch: Partial<Pick<EditorPreferences, 'warnUnusedEntries' | 'warnIncompleteEntries'>>,
  ) => void
}

interface NodeNameDraft {
  kind: 'entry' | 'folder'
  name: string
  isSaving: boolean
  error?: string
}

interface NodeDraft extends NodeNameDraft {
  parentKey: Key
  parentPath: string
}

interface NodeRenameDraft extends NodeNameDraft {
  key: Key
  path: string
}

interface LocaleDraft {
  id: string
  displayName: string
  culture: string
  isSaving: boolean
  error?: string
}

const nodeDraftKey = 'draft:node'
const pathSegmentPattern = /^[A-Za-z_][A-Za-z0-9_]*$/
// Ant Design treats null as "use the default motion"; rc-tree requires false to skip motion rows.
const disabledTreeMotion = false as unknown as TreeProps['motion']

export function CatalogTreePanel({
  tree,
  selectedKeys,
  expandedKeys,
  onSelectionChange,
  onExpandedKeysChange,
  onAddEntry,
  onAddLocale,
  onAddFolder,
  onRemoveNodes,
  onMoveNodes,
  onRenameNode,
  warningPreferences,
  usageWarningsAvailable,
  preferencesError,
  arePreferencesBusy,
  onWarningPreferencesChange,
}: CatalogTreePanelProps) {
  const { token } = theme.useToken()
  const [messageApi, messageContext] = message.useMessage()
  const [modalApi, modalContext] = Modal.useModal()
  const [query, setQuery] = useState('')
  const [selectionAnchor, setSelectionAnchor] = useState<Key>()
  const [nodeDraft, setNodeDraft] = useState<NodeDraft>()
  const [nodeRenameDraft, setNodeRenameDraft] = useState<NodeRenameDraft>()
  const [localeDraft, setLocaleDraft] = useState<LocaleDraft>()
  const [pendingRevealKey, setPendingRevealKey] = useState<Key>()
  const [revealedKey, setRevealedKey] = useState<Key>()
  const [contextMenuNode, setContextMenuNode] = useState<CatalogTreeNode>()
  const [isContextMenuOpen, setIsContextMenuOpen] = useState(false)
  const treeContainerRef = useRef<HTMLDivElement>(null)
  const treeRef = useRef<ComponentRef<typeof Tree>>(null)
  const [treeHeight, setTreeHeight] = useState(() =>
    typeof window === 'undefined' ? 600 : window.innerHeight)
  const hasQuery = Boolean(query.trim())
  const matchingKeys = useMemo(
    () => findMatchingCatalogKeys(tree.searchRecords, query),
    [query, tree.searchRecords],
  )
  const filteredNodes = useMemo(
    () => hasQuery
      ? matchingKeys ? filterCatalogTree(tree.nodes, matchingKeys) : []
      : tree.nodes,
    [hasQuery, matchingKeys, tree.nodes],
  )
  const nodes = useMemo(
    () => nodeDraft ? insertNodeDraft(filteredNodes, nodeDraft) : filteredNodes,
    [filteredNodes, nodeDraft],
  )
  const visibleExpandedKeys = useMemo(
    () => hasQuery ? collectExpandableKeys(nodes) : expandedKeys,
    [expandedKeys, hasQuery, nodes],
  )

  useLayoutEffect(() => {
    const container = treeContainerRef.current
    if (!container) {
      return
    }

    const updateHeight = () => {
      const nextHeight = Math.max(1, Math.floor(container.getBoundingClientRect().height))
      setTreeHeight((currentHeight) => currentHeight === nextHeight ? currentHeight : nextHeight)
    }
    updateHeight()

    const resizeObserver = new ResizeObserver(updateHeight)
    resizeObserver.observe(container)
    return () => resizeObserver.disconnect()
  }, [])

  const revealInTree = (key?: Key) => {
    setQuery('')
    if (key === undefined) {
      return
    }

    const ancestorKeys = findAncestorKeys(tree.nodes, key)
    if (!ancestorKeys) {
      return
    }

    onExpandedKeysChange(mergeKeys(expandedKeys, ancestorKeys))
    setPendingRevealKey(key)
  }

  const revealCurrentSelection = () => {
    revealInTree(selectionAnchor ?? selectedKeys.at(-1))
  }

  useEffect(() => {
    if (pendingRevealKey === undefined || query.trim()) {
      return
    }

    treeRef.current?.scrollTo({ key: pendingRevealKey })
    setRevealedKey(pendingRevealKey)
    setPendingRevealKey(undefined)
  }, [expandedKeys, pendingRevealKey, query])

  useEffect(() => {
    if (revealedKey === undefined) {
      return
    }

    const timeout = window.setTimeout(() => setRevealedKey(undefined), 1200)
    return () => window.clearTimeout(timeout)
  }, [revealedKey])

  const requestDeletion = (keys: Key[]) => {
    const deletableKeys = keys.filter((key) => {
      const item = tree.selectionByKey.get(String(key))
      return item?.kind === 'entry' || item?.kind === 'folder' || item?.kind === 'locale'
    })
    if (deletableKeys.length === 0) {
      return
    }

    const description = describeDeletion(tree, deletableKeys)
    modalApi.confirm({
      title: description.title,
      content: description.content,
      okText: 'Delete',
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await onRemoveNodes(deletableKeys)
        } catch (error: unknown) {
          messageApi.error(error instanceof Error ? error.message : 'Selection could not be deleted.')
          throw error
        }
      },
    })
  }

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Delete' || isTextEditingTarget(event.target)) {
        return
      }

      const hasDeletableSelection = selectedKeys.some((key) => {
        const item = tree.selectionByKey.get(String(key))
        return item?.kind === 'entry' || item?.kind === 'folder' || item?.kind === 'locale'
      })
      if (!hasDeletableSelection) {
        return
      }

      event.preventDefault()
      requestDeletion(selectedKeys)
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  })

  const beginCreation = (node: CatalogTreeNode, kind: NodeDraft['kind']) => {
    if (node.kind !== 'entries-root' && node.kind !== 'folder') {
      return
    }

    setQuery('')
    setNodeRenameDraft(undefined)
    onExpandedKeysChange(mergeKeys(expandedKeys, [node.key]))
    setNodeDraft({
      kind,
      parentKey: node.key,
      parentPath: node.path ?? '',
      name: kind === 'entry' ? 'NewEntry' : 'NewFolder',
      isSaving: false,
    })
  }

  const beginRename = (node: CatalogTreeNode) => {
    if ((node.kind !== 'entry' && node.kind !== 'folder') || !node.path) {
      return
    }

    setQuery('')
    setNodeDraft(undefined)
    setNodeRenameDraft({
      key: node.key,
      kind: node.kind,
      path: node.path,
      name: getLastPathSegment(node.path),
      isSaving: false,
    })
  }

  const handleAction = async (node: CatalogTreeNode, action: string) => {
    if (action === 'reveal-in-tree') {
      revealInTree(node.key)
    } else if (action === 'new-entry') {
      beginCreation(node, 'entry')
    } else if (action === 'new-folder') {
      beginCreation(node, 'folder')
    } else if (action === 'new-locale') {
      setLocaleDraft({
        id: createNewLocaleId(tree),
        displayName: 'New locale',
        culture: 'en-US',
        isSaving: false,
      })
    } else if (action === 'rename-entry' || action === 'rename-folder') {
      beginRename(node)
    } else if (action === 'copy-entry-id' && node.entryId) {
      try {
        await navigator.clipboard.writeText(node.entryId)
        messageApi.success('Entry ID copied.')
      } catch {
        messageApi.error('Entry ID could not be copied.')
      }
    } else if (action === 'delete-entry' || action === 'delete-folder' || action === 'delete-locale') {
      const keys = selectedKeys.includes(node.key) ? selectedKeys : [node.key]
      requestDeletion(keys)
    }
  }

  const submitLocaleDraft = async () => {
    if (!localeDraft || localeDraft.isSaving) {
      return
    }

    const id = localeDraft.id.trim()
    const displayName = localeDraft.displayName.trim()
    const culture = localeDraft.culture.trim()
    const error = validateLocaleDraft(id, displayName, culture, tree)
    if (error) {
      setLocaleDraft({ ...localeDraft, id, displayName, culture, error })
      return
    }

    setLocaleDraft({ ...localeDraft, id, displayName, culture, isSaving: true, error: undefined })
    try {
      const key = await onAddLocale({
        id,
        displayName,
        culture,
        fallback: null,
        icon: null,
      })
      setLocaleDraft(undefined)
      onSelectionChange([key])
    } catch (reason: unknown) {
      setLocaleDraft({
        ...localeDraft,
        id,
        displayName,
        culture,
        isSaving: false,
        error: reason instanceof Error ? reason.message : 'Locale could not be added.',
      })
    }
  }

  const submitNodeDraft = async () => {
    if (!nodeDraft || nodeDraft.isSaving) {
      return
    }

    const name = nodeDraft.name.trim()
    const error = validateNodeName(name, nodeDraft.kind)
    if (error) {
      setNodeDraft({ ...nodeDraft, name, error })
      return
    }

    const path = nodeDraft.parentPath ? `${nodeDraft.parentPath}.${name}` : name
    setNodeDraft({ ...nodeDraft, name, isSaving: true, error: undefined })

    try {
      const createdKey = nodeDraft.kind === 'entry'
        ? await onAddEntry(path)
        : onAddFolder(path)
      setNodeDraft(undefined)
      onSelectionChange([createdKey])
    } catch (error: unknown) {
      setNodeDraft({
        ...nodeDraft,
        name,
        isSaving: false,
        error: error instanceof Error ? error.message : 'Entry could not be created.',
      })
    }
  }

  const submitNodeRename = async () => {
    if (!nodeRenameDraft || nodeRenameDraft.isSaving) {
      return
    }

    const name = nodeRenameDraft.name.trim()
    const error = validateNodeName(name, nodeRenameDraft.kind)
    if (error) {
      setNodeRenameDraft({ ...nodeRenameDraft, name, error })
      return
    }

    if (name === getLastPathSegment(nodeRenameDraft.path)) {
      setNodeRenameDraft(undefined)
      return
    }

    setNodeRenameDraft({ ...nodeRenameDraft, name, isSaving: true, error: undefined })
    try {
      const renamedKey = await onRenameNode(nodeRenameDraft.key, name)
      setNodeRenameDraft(undefined)
      onSelectionChange([renamedKey])
    } catch (error: unknown) {
      setNodeRenameDraft({
        ...nodeRenameDraft,
        name,
        isSaving: false,
        error: error instanceof Error ? error.message : 'Item could not be renamed.',
      })
    }
  }

  const handleDraftChange = useCallback((name: string) => setNodeDraft((draft) =>
    draft ? { ...draft, name, error: undefined } : draft), [])
  const handleDraftCancel = useCallback(() => setNodeDraft(undefined), [])
  const handleRenameChange = useCallback((name: string) => setNodeRenameDraft((draft) =>
    draft ? { ...draft, name, error: undefined } : draft), [])
  const handleRenameCancel = useCallback(() => setNodeRenameDraft(undefined), [])
  const handleDraftSubmit = useLatestCallback(submitNodeDraft)
  const handleRenameSubmit = useLatestCallback(submitNodeRename)
  const handleNodeAction = useLatestCallback(handleAction)

  return (
    <Flex
      vertical
      gap={layoutTokens.spacing.small}
      style={{ height: '100%', minHeight: 0 }}
    >
      {messageContext}
      {modalContext}
      <Modal
        open={Boolean(localeDraft)}
        title="New locale"
        okText="Add locale"
        confirmLoading={localeDraft?.isSaving}
        onOk={() => void submitLocaleDraft()}
        onCancel={() => {
          if (!localeDraft?.isSaving) {
            setLocaleDraft(undefined)
          }
        }}
      >
        {localeDraft && (
          <Flex vertical gap={layoutTokens.spacing.medium}>
            <Space.Compact block>
              <Space.Addon style={{ flex: '0 0 112px', justifyContent: 'flex-start' }}>ID</Space.Addon>
              <Input
                autoFocus
                value={localeDraft.id}
                style={{ flex: '1 1 0', minWidth: 0 }}
                status={localeDraft.error ? 'error' : undefined}
                disabled={localeDraft.isSaving}
                onChange={(event) => setLocaleDraft({
                  ...localeDraft,
                  id: event.target.value,
                  error: undefined,
                })}
              />
            </Space.Compact>
            <Space.Compact block>
              <Space.Addon style={{ flex: '0 0 112px', justifyContent: 'flex-start' }}>Display name</Space.Addon>
              <Input
                value={localeDraft.displayName}
                style={{ flex: '1 1 0', minWidth: 0 }}
                disabled={localeDraft.isSaving}
                onChange={(event) => setLocaleDraft({
                  ...localeDraft,
                  displayName: event.target.value,
                  error: undefined,
                })}
              />
            </Space.Compact>
            <Space.Compact block>
              <Space.Addon style={{ flex: '0 0 112px', justifyContent: 'flex-start' }}>Culture</Space.Addon>
              <AutoComplete
                value={localeDraft.culture}
                options={commonCultureOptions}
                disabled={localeDraft.isSaving}
                style={{ flex: '1 1 0', minWidth: 0 }}
                filterOption={(inputValue, option) =>
                  (option?.value ?? '').toLowerCase().includes(inputValue.toLowerCase())}
                onChange={(culture) => setLocaleDraft({
                  ...localeDraft,
                  culture,
                  error: undefined,
                })}
              />
            </Space.Compact>
            {localeDraft.error && <Typography.Text type="danger">{localeDraft.error}</Typography.Text>}
          </Flex>
        )}
      </Modal>
      <Flex gap={layoutTokens.spacing.small}>
        <Input.Search
          allowClear
          value={query}
          placeholder="Search paths, IDs, and localized texts"
          style={{ flex: 1, minWidth: 0 }}
          onChange={(event) => {
            if (!event.target.value && query) {
              revealCurrentSelection()
            } else {
              setQuery(event.target.value)
            }
          }}
          onKeyDown={(event) => {
            if (event.key === 'Escape' && query) {
              event.preventDefault()
              revealCurrentSelection()
            }
          }}
        />
        <Popover
          trigger="click"
          placement="bottomRight"
          title="Hierarchy warnings"
          content={(
            <Flex vertical gap={layoutTokens.spacing.medium} style={{ width: 300 }}>
              <Flex align="flex-start" justify="space-between" gap={layoutTokens.spacing.large}>
                <Flex vertical gap={layoutTokens.spacing.xSmall} style={{ minWidth: 0 }}>
                  <Typography.Text>Unused entries</Typography.Text>
                  <Typography.Text type="secondary">
                    Mark entries with no indexed Unity usages.
                  </Typography.Text>
                  {warningPreferences.warnUnusedEntries && !usageWarningsAvailable && (
                    <Typography.Text type="secondary">Usage data unavailable</Typography.Text>
                  )}
                </Flex>
                <Switch
                  size="small"
                  checked={warningPreferences.warnUnusedEntries}
                  loading={arePreferencesBusy}
                  disabled={arePreferencesBusy}
                  onChange={(warnUnusedEntries) =>
                    onWarningPreferencesChange({ warnUnusedEntries })}
                />
              </Flex>
              <Flex align="flex-start" justify="space-between" gap={layoutTokens.spacing.large}>
                <Flex vertical gap={layoutTokens.spacing.xSmall} style={{ minWidth: 0 }}>
                  <Typography.Text>Incomplete localization</Typography.Text>
                  <Typography.Text type="secondary">
                    Mark entries missing content in one or more locales.
                  </Typography.Text>
                </Flex>
                <Switch
                  size="small"
                  checked={warningPreferences.warnIncompleteEntries}
                  loading={arePreferencesBusy}
                  disabled={arePreferencesBusy}
                  onChange={(warnIncompleteEntries) =>
                    onWarningPreferencesChange({ warnIncompleteEntries })}
                />
              </Flex>
              {preferencesError && (
                <Typography.Text type="danger">{preferencesError}</Typography.Text>
              )}
            </Flex>
          )}
        >
          <Button
            icon={<SettingOutlined />}
            aria-label="Hierarchy warning settings"
            style={{ width: token.controlHeight, paddingInline: 0 }}
          />
        </Popover>
      </Flex>
      <Dropdown
        open={isContextMenuOpen}
        trigger={['contextMenu']}
        menu={{
          items: contextMenuNode ? getContextMenuItems(contextMenuNode, hasQuery) : [],
          onClick: ({ key, domEvent }) => {
            domEvent.stopPropagation()
            setIsContextMenuOpen(false)
            if (contextMenuNode) {
              void handleAction(contextMenuNode, key)
            }
          },
        }}
        onOpenChange={(open) => {
          if (!open) {
            setIsContextMenuOpen(false)
            setContextMenuNode(undefined)
          }
        }}
      >
        <div ref={treeContainerRef} style={{ flex: 1, minHeight: 0, overflow: 'hidden' }}>
          <Tree<CatalogTreeNode>
            ref={treeRef}
            blockNode
            virtual
            height={treeHeight}
            multiple
            motion={disabledTreeMotion}
            autoExpandParent={hasQuery}
            expandedKeys={visibleExpandedKeys}
            selectedKeys={selectedKeys}
            treeData={nodes}
            draggable={{
            icon: false,
            nodeDraggable: (node) => {
              const kind = (node as CatalogTreeNode).kind
              return !hasQuery && (kind === 'folder' || kind === 'entry')
            },
          }}
          allowDrop={({ dragNode, dropNode, dropPosition }) => {
            const dragKey = dragNode.key
            const keys = selectedKeys.includes(dragKey) ? selectedKeys : [dragKey]
            return dropPosition === 0 && canDropInto(tree, keys, dropNode as CatalogTreeNode)
          }}
          styles={{
            root: { background: 'transparent', borderRadius: 0 },
            itemTitle: {
              display: 'inline-block',
              width: `calc(100% + ${token.paddingXS}px)`,
              minWidth: 0,
              marginInlineEnd: -token.paddingXS,
            },
          }}
          titleRender={(node) => (
            <TreeNodeTitle
              node={node as CatalogTreeNode}
              iconColor={getCatalogNodeIconColor((node as CatalogTreeNode).kind, token)}
              dirtyColor={token.colorWarning}
              infoColor={token.colorInfo}
              warningColor={token.colorWarning}
              secondaryColor={token.colorTextSecondary}
              revealColor={token.colorWarningBg}
              borderRadius={token.borderRadiusSM}
              rowHeight={token.controlHeightSM}
              isRevealed={revealedKey === node.key}
              nodeDraft={nodeDraft}
              nodeRenameDraft={nodeRenameDraft}
              onDraftChange={handleDraftChange}
              onDraftSubmit={handleDraftSubmit}
              onDraftCancel={handleDraftCancel}
              onRenameChange={handleRenameChange}
              onRenameSubmit={handleRenameSubmit}
              onRenameCancel={handleRenameCancel}
              onAction={handleNodeAction}
            />
          )}
          onExpand={(nextExpandedKeys, info) => {
            if (!hasQuery) {
              if (info.nativeEvent.altKey) {
                const branchKeys: Key[] = [
                  ...(tree.expandableKeysByKey.get(String(info.node.key)) ?? []),
                ]
                const branchKeySet = new Set<Key>(branchKeys)
                onExpandedKeysChange(info.expanded
                  ? mergeKeys(expandedKeys, branchKeys)
                  : expandedKeys.filter((key) => !branchKeySet.has(key)))
                return
              }

              onExpandedKeysChange(nextExpandedKeys)
            }
          }}
          onDragStart={(info) => {
            const key = info.node.key
            if (!selectedKeys.includes(key)) {
              setSelectionAnchor(key)
              onSelectionChange([key])
            }
          }}
          onDragEnter={(info) => {
            if (!hasQuery) {
              onExpandedKeysChange(info.expandedKeys)
            }
          }}
          onDrop={(info) => {
            if (info.dropToGap) {
              return
            }

            const dragKey = info.dragNode.key
            const keys = selectedKeys.includes(dragKey) ? selectedKeys : [dragKey]
            void onMoveNodes(keys, info.node.key).catch((error: unknown) => {
              messageApi.error(error instanceof Error ? error.message : 'Selection could not be moved.')
            })
          }}
          onSelect={(_, info) => {
            const key = info.node.key
            const event = info.nativeEvent

            if (event.shiftKey && selectionAnchor !== undefined) {
              const visibleSelectionKeys = collectVisibleSelectionKeys(
                nodes,
                new Set(visibleExpandedKeys),
              )
              const range = getSelectionRange(visibleSelectionKeys, selectionAnchor, key)
              onSelectionChange(event.ctrlKey || event.metaKey
                ? mergeKeys(selectedKeys, range)
                : range)
              return
            }

            setSelectionAnchor(key)

            if (event.ctrlKey || event.metaKey) {
              onSelectionChange(selectedKeys.includes(key)
                ? selectedKeys.filter((selectedKey) => selectedKey !== key)
                : [...selectedKeys, key])
              return
            }

            if (selectedKeys.length === 1 && selectedKeys[0] === key) {
              setSelectionAnchor(undefined)
              onSelectionChange([])
              return
            }

            onSelectionChange([key])
          }}
          onRightClick={({ event, node }) => {
            event.preventDefault()
            setContextMenuNode(node as CatalogTreeNode)
            setIsContextMenuOpen(true)
          }}
          />
        </div>
      </Dropdown>
    </Flex>
  )
}

function describeDeletion(tree: CatalogTreeModel, keys: Key[]) {
  if (keys.length !== 1) {
    return {
      title: `Delete ${keys.length} selected items?`,
      content: 'Selected locales, folders, entries, and their contained values will be removed from the working copy.',
    }
  }

  const item = tree.selectionByKey.get(String(keys[0]))!
  if (item.kind === 'entry') {
    return {
      title: `Delete '${getLastPathSegment(item.entry.path)}'?`,
      content: 'The entry will be removed from the working copy.',
    }
  }

  if (item.kind === 'locale') {
    return {
      title: `Delete locale '${item.locale.displayName}'?`,
      content: 'The locale and all of its localized values will be removed from the working copy.',
    }
  }

  if (item.kind !== 'folder') {
    throw new Error('The selected item cannot be deleted from the catalog tree.')
  }

  return {
    title: `Delete folder '${getLastPathSegment(item.path)}'?`,
    content: item.entryCount === 0
      ? 'The empty folder will be removed from the working copy.'
      : `${item.entryCount} ${item.entryCount === 1 ? 'entry' : 'entries'} inside the folder will also be removed.`,
  }
}

function getLastPathSegment(path: string) {
  return path.slice(path.lastIndexOf('.') + 1)
}

function isTextEditingTarget(target: EventTarget | null) {
  return target instanceof HTMLInputElement ||
    target instanceof HTMLTextAreaElement ||
    target instanceof HTMLSelectElement ||
    target instanceof HTMLElement && target.isContentEditable
}

function canDropInto(tree: CatalogTreeModel, dragKeys: Key[], target: CatalogTreeNode) {
  if (target.kind !== 'entries-root' && target.kind !== 'folder') {
    return false
  }

  const targetPath = target.path ?? ''
  return !dragKeys.some((key) => {
    const item = tree.selectionByKey.get(String(key))
    return item?.kind === 'folder' &&
      (targetPath === item.path || targetPath.startsWith(`${item.path}.`))
  })
}

const TreeNodeTitle = memo(function TreeNodeTitle({
  node,
  iconColor,
  dirtyColor,
  infoColor,
  warningColor,
  secondaryColor,
  revealColor,
  borderRadius,
  rowHeight,
  isRevealed,
  nodeDraft,
  nodeRenameDraft,
  onDraftChange,
  onDraftSubmit,
  onDraftCancel,
  onRenameChange,
  onRenameSubmit,
  onRenameCancel,
  onAction,
}: {
  node: CatalogTreeNode
  iconColor: string
  dirtyColor: string
  infoColor: string
  warningColor: string
  secondaryColor: string
  revealColor: string
  borderRadius: number
  rowHeight: number
  isRevealed: boolean
  nodeDraft?: NodeDraft
  nodeRenameDraft?: NodeRenameDraft
  onDraftChange: (name: string) => void
  onDraftSubmit: () => void
  onDraftCancel: () => void
  onRenameChange: (name: string) => void
  onRenameSubmit: () => void
  onRenameCancel: () => void
  onAction: (node: CatalogTreeNode, action: string) => void
}) {
  const quickActions = getQuickActions(node.kind)

  if ((node.kind === 'entry-draft' || node.kind === 'folder-draft') && nodeDraft) {
    return (
      <DraftNodeTitle
        draft={nodeDraft}
        iconColor={iconColor}
        rowHeight={rowHeight}
        onChange={onDraftChange}
        onSubmit={onDraftSubmit}
        onCancel={onDraftCancel}
      />
    )
  }

  if (nodeRenameDraft?.key === node.key) {
    return (
      <DraftNodeTitle
        draft={nodeRenameDraft}
        iconColor={iconColor}
        rowHeight={rowHeight}
        onChange={onRenameChange}
        onSubmit={onRenameSubmit}
        onCancel={onRenameCancel}
      />
    )
  }

  return (
      <span
        className={quickActions.length > 0
          ? 'catalog-tree-node-title catalog-tree-node-title--has-actions'
          : 'catalog-tree-node-title'}
        style={{
          display: 'inline-flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: layoutTokens.spacing.small,
          width: '100%',
          height: rowHeight,
          minWidth: 0,
          backgroundColor: isRevealed ? revealColor : 'transparent',
          borderRadius,
          transition: 'background-color 600ms ease-out',
        }}
      >
        <span
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: layoutTokens.spacing.small,
            minWidth: 0,
          }}
        >
          {node.kind === 'locale' && node.culture
            ? <LocaleFlag culture={node.culture} />
            : renderCatalogNodeIcon(node.kind, iconColor)}
          <span
            style={{
              color: node.isDirty ? dirtyColor : node.isTemporary ? secondaryColor : undefined,
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
            }}
          >
            {node.title}
          </span>
        </span>
        {quickActions.length > 0 && (
          <span
            className="catalog-tree-node-title__actions"
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              height: '100%',
              flex: '0 0 auto',
            }}
          >
            {quickActions.map((action) => (
              <Tooltip key={action.key} title={action.label} mouseEnterDelay={0.4}>
                <Button
                  type="text"
                  size="small"
                  icon={action.icon}
                  aria-label={action.label}
                  style={{ width: rowHeight, minWidth: rowHeight, height: '100%', padding: 0 }}
                  onMouseDown={(event) => event.stopPropagation()}
                  onClick={(event) => {
                    event.stopPropagation()
                    onAction(node, action.key)
                  }}
                />
              </Tooltip>
            ))}
          </span>
        )}
        {node.hasWarning ? (
          <span
            className="catalog-tree-node-title__status"
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: layoutTokens.spacing.small,
              flex: '0 0 auto',
            }}
          >
            {node.kind === 'entry' && !node.hasUnusedWarning && node.count !== undefined && (
              <span style={{ color: infoColor, fontSize: 12 }}>
                {node.count}
              </span>
            )}
            {node.kind === 'entry' && node.warningMessages ? (
              <span title={node.warningMessages.join('\n')}>
                <WarningOutlined
                  aria-label={node.warningMessages.join('. ')}
                  style={{ color: warningColor }}
                />
              </span>
            ) : (
              <WarningOutlined
                aria-label="Contains an entry with a warning"
                style={{ color: warningColor }}
              />
            )}
          </span>
        ) : node.count !== undefined ? (
          <span
            className="catalog-tree-node-title__status"
            style={{
              color: node.kind === 'entry' ? infoColor : secondaryColor,
              flex: '0 0 auto',
              fontSize: 12,
            }}
          >
            {node.count}
          </span>
        ) : null}
      </span>
  )
})

function DraftNodeTitle({
  draft,
  iconColor,
  rowHeight,
  onChange,
  onSubmit,
  onCancel,
}: {
  draft: NodeNameDraft
  iconColor: string
  rowHeight: number
  onChange: (name: string) => void
  onSubmit: () => void
  onCancel: () => void
}) {
  const inputRef = useRef<InputRef>(null)

  useEffect(() => {
    inputRef.current?.focus()
    inputRef.current?.select()
  }, [])

  return (
    <Tooltip title={draft.error} open={draft.error ? undefined : false} placement="right">
      <span
        style={{
          display: 'inline-flex',
          alignItems: 'center',
          gap: layoutTokens.spacing.small,
          width: '100%',
          height: rowHeight,
        }}
        onMouseDown={(event) => event.stopPropagation()}
      >
        {renderCatalogNodeIcon(draft.kind === 'entry' ? 'entry-draft' : 'folder-draft', iconColor)}
        <Input
          ref={inputRef}
          size="small"
          value={draft.name}
          status={draft.error ? 'error' : undefined}
          disabled={draft.isSaving}
          onChange={(event) => onChange(event.target.value)}
          onPressEnter={onSubmit}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.stopPropagation()
            } else if (event.key === 'Escape') {
              event.preventDefault()
              event.stopPropagation()
              onCancel()
            }
          }}
        />
      </span>
    </Tooltip>
  )
}

interface QuickAction {
  key: string
  label: string
  icon: ReactNode
}

function getQuickActions(kind: CatalogTreeNode['kind']): QuickAction[] {
  switch (kind) {
    case 'locales-root':
      return [{ key: 'new-locale', label: 'New locale', icon: <PlusOutlined /> }]
    case 'entries-root':
    case 'folder':
      return [
        { key: 'new-entry', label: 'New entry', icon: <FileAddOutlined /> },
        { key: 'new-folder', label: 'New folder', icon: <FolderAddOutlined /> },
      ]
    case 'locale':
    case 'entry':
    case 'entry-draft':
    case 'folder-draft':
      return []
  }
}

function getContextMenuItems(node: CatalogTreeNode, canReveal: boolean): MenuProps['items'] {
  const revealItems: MenuProps['items'] = canReveal
    ? [
        { key: 'reveal-in-tree', icon: <AimOutlined />, label: 'Reveal in tree' },
        { type: 'divider' },
      ]
    : []

  switch (node.kind) {
    case 'locales-root':
      return [
        { key: 'new-locale', icon: <PlusOutlined />, label: 'New locale' },
      ]
    case 'locale':
      return [
        ...revealItems,
        {
          key: 'delete-locale',
          icon: <DeleteOutlined />,
          label: 'Delete',
          extra: <ShortcutHint>Del</ShortcutHint>,
          danger: true,
        },
      ]
    case 'entries-root':
      return createContainerMenuItems(false, false)
    case 'folder':
      return createContainerMenuItems(true, canReveal)
    case 'entry':
      return [
        ...revealItems,
        { key: 'rename-entry', icon: <EditOutlined />, label: 'Rename' },
        { key: 'copy-entry-id', icon: <CopyOutlined />, label: 'Copy ID' },
        { type: 'divider' },
        {
          key: 'delete-entry',
          icon: <DeleteOutlined />,
          label: 'Delete',
          extra: <ShortcutHint>Del</ShortcutHint>,
          danger: true,
        },
      ]
    case 'entry-draft':
    case 'folder-draft':
      return []
  }
}

function createContainerMenuItems(
  includeOwnActions: boolean,
  canReveal: boolean,
): MenuProps['items'] {
  return [
    ...(canReveal
      ? [
          { key: 'reveal-in-tree', icon: <AimOutlined />, label: 'Reveal in tree' },
          { type: 'divider' as const },
        ]
      : []),
    { key: 'new-entry', icon: <FileAddOutlined />, label: 'New entry' },
    { key: 'new-folder', icon: <FolderAddOutlined />, label: 'New folder' },
    ...(includeOwnActions
      ? [
          { type: 'divider' as const },
          { key: 'rename-folder', icon: <EditOutlined />, label: 'Rename' },
          {
            key: 'delete-folder',
            icon: <DeleteOutlined />,
            label: 'Delete',
            extra: <ShortcutHint>Del</ShortcutHint>,
            danger: true,
          },
        ]
      : []),
  ]
}

function ShortcutHint({ children }: { children: ReactNode }) {
  return (
    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
      {children}
    </Typography.Text>
  )
}

function collectExpandableKeys(nodes: CatalogTreeNode[]): Key[] {
  return nodes.flatMap((node) => [
    ...(node.children?.length ? [node.key] : []),
    ...collectExpandableKeys(node.children ?? []),
  ])
}

function findAncestorKeys(
  nodes: CatalogTreeNode[],
  targetKey: Key,
  ancestors: Key[] = [],
): Key[] | undefined {
  for (const node of nodes) {
    if (node.key === targetKey) {
      return ancestors
    }

    const result = findAncestorKeys(node.children ?? [], targetKey, [...ancestors, node.key])
    if (result) {
      return result
    }
  }

  return undefined
}

function collectVisibleSelectionKeys(nodes: CatalogTreeNode[], expandedKeys: ReadonlySet<Key>): Key[] {
  return nodes.flatMap((node) => [
    ...(node.selectable === false ? [] : [node.key]),
    ...(node.children?.length && expandedKeys.has(node.key)
      ? collectVisibleSelectionKeys(node.children, expandedKeys)
      : []),
  ])
}

function getSelectionRange(keys: Key[], anchor: Key, target: Key): Key[] {
  const anchorIndex = keys.indexOf(anchor)
  const targetIndex = keys.indexOf(target)

  if (anchorIndex < 0 || targetIndex < 0) {
    return [target]
  }

  const start = Math.min(anchorIndex, targetIndex)
  const end = Math.max(anchorIndex, targetIndex)
  return keys.slice(start, end + 1)
}

function mergeKeys(current: Key[], added: Key[]): Key[] {
  return [...new Set([...current, ...added])]
}

function useLatestCallback<Arguments extends unknown[], Result>(
  callback: (...args: Arguments) => Result,
) {
  const callbackRef = useRef(callback)
  callbackRef.current = callback

  return useCallback((...args: Arguments) => callbackRef.current(...args), [])
}

function insertNodeDraft(nodes: CatalogTreeNode[], draft: NodeDraft): CatalogTreeNode[] {
  return nodes.map((node) => {
    if (node.key === draft.parentKey) {
      return {
        ...node,
        children: [
          ...(node.children ?? []),
          {
            key: nodeDraftKey,
            title: draft.name,
            kind: draft.kind === 'entry' ? 'entry-draft' : 'folder-draft',
            searchText: draft.name,
            selectable: false,
          },
        ],
      }
    }

    return node.children?.length
      ? { ...node, children: insertNodeDraft(node.children, draft) }
      : node
  })
}

function capitalize(value: string) {
  return value.charAt(0).toUpperCase() + value.slice(1)
}

function validateNodeName(name: string, kind: 'entry' | 'folder') {
  if (!name) {
    return `${capitalize(kind)} name is required.`
  }

  return pathSegmentPattern.test(name)
    ? undefined
    : 'The name must start with a Latin letter or underscore and contain only letters, digits, and underscores.'
}

function createNewLocaleId(tree: CatalogTreeModel) {
  const ids = new Set([...tree.selectionByKey.values()]
    .filter((item) => item.kind === 'locale')
    .map((item) => item.locale.id))
  let suffix = 1
  while (ids.has(suffix === 1 ? 'new-locale' : `new-locale-${suffix}`)) {
    suffix++
  }

  return suffix === 1 ? 'new-locale' : `new-locale-${suffix}`
}

function validateLocaleDraft(
  id: string,
  displayName: string,
  culture: string,
  tree: CatalogTreeModel,
) {
  if (!/^[A-Za-z][A-Za-z0-9]*(?:-[A-Za-z0-9]+)*$/.test(id)) {
    return 'ID must use hyphen-separated ASCII letter and digit segments.'
  }

  if ([...tree.selectionByKey.values()].some((item) =>
    item.kind === 'locale' && item.locale.id === id)) {
    return `Locale ID '${id}' is already used.`
  }

  if (!displayName) {
    return 'Display name is required.'
  }

  if (!culture) {
    return 'Culture is required.'
  }

  return undefined
}
