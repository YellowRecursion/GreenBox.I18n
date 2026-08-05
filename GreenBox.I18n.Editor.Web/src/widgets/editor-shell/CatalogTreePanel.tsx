import {
  useEffect,
  useMemo,
  useRef,
  useState,
  type Key,
  type MouseEvent as ReactMouseEvent,
  type ReactNode,
} from 'react'
import { createPortal } from 'react-dom'
import {
  AimOutlined,
  CopyOutlined,
  DeleteOutlined,
  EditOutlined,
  FileAddOutlined,
  FolderAddOutlined,
  PlusOutlined,
} from '@ant-design/icons'
import {
  Button,
  Dropdown,
  Flex,
  Input,
  Modal,
  Tooltip,
  Tree,
  Typography,
  message,
  theme,
  type InputRef,
  type MenuProps,
} from 'antd'
import { layoutTokens } from '../../design/layoutTokens'
import { filterCatalogTree, type CatalogTreeModel, type CatalogTreeNode } from './catalogTree'
import { getCatalogNodeIconColor, renderCatalogNodeIcon } from './catalogNodeVisuals'

interface CatalogTreePanelProps {
  tree: CatalogTreeModel
  selectedKeys: Key[]
  expandedKeys: Key[]
  onSelectionChange: (keys: Key[]) => void
  onExpandedKeysChange: (keys: Key[]) => void
  onAddEntry: (path: string) => Promise<string>
  onAddFolder: (path: string) => string
  onRemoveNodes: (keys: Key[]) => Promise<void>
  onMoveNodes: (keys: Key[], targetKey: Key) => Promise<void>
  onRenameNode: (key: Key, name: string) => Promise<string>
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

const nodeDraftKey = 'draft:node'
const pathSegmentPattern = /^[A-Za-z_][A-Za-z0-9_]*$/

export function CatalogTreePanel({
  tree,
  selectedKeys,
  expandedKeys,
  onSelectionChange,
  onExpandedKeysChange,
  onAddEntry,
  onAddFolder,
  onRemoveNodes,
  onMoveNodes,
  onRenameNode,
}: CatalogTreePanelProps) {
  const { token } = theme.useToken()
  const [messageApi, messageContext] = message.useMessage()
  const [modalApi, modalContext] = Modal.useModal()
  const [query, setQuery] = useState('')
  const [selectionAnchor, setSelectionAnchor] = useState<Key>()
  const [nodeDraft, setNodeDraft] = useState<NodeDraft>()
  const [nodeRenameDraft, setNodeRenameDraft] = useState<NodeRenameDraft>()
  const [pendingRevealKey, setPendingRevealKey] = useState<Key>()
  const [revealedKey, setRevealedKey] = useState<Key>()
  const treeContainerRef = useRef<HTMLDivElement>(null)
  const filteredNodes = useMemo(() => filterCatalogTree(tree.nodes, query), [tree.nodes, query])
  const nodes = useMemo(
    () => nodeDraft ? insertNodeDraft(filteredNodes, nodeDraft) : filteredNodes,
    [filteredNodes, nodeDraft],
  )
  const visibleExpandedKeys = query.trim()
    ? collectExpandableKeys(nodes)
    : expandedKeys
  const visibleSelectionKeys = collectVisibleSelectionKeys(nodes, new Set(visibleExpandedKeys))

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

    const element = treeContainerRef.current?.querySelector<HTMLElement>(
      `[data-node-key="${CSS.escape(String(pendingRevealKey))}"]`,
    )
    element?.scrollIntoView({ block: 'nearest' })
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
      return item?.kind === 'entry' || item?.kind === 'folder'
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
        return item?.kind === 'entry' || item?.kind === 'folder'
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

  const handlePanelMouseDown = (event: ReactMouseEvent<HTMLElement>) => {
    if (event.button !== 0) {
      return
    }

    const target = event.target as HTMLElement
    if (target.closest('.ant-tree-treenode, .ant-input-affix-wrapper, button')) {
      return
    }

    setSelectionAnchor(undefined)
    onSelectionChange([])
  }

  const handleAction = async (node: CatalogTreeNode, action: string) => {
    if (action === 'reveal-in-tree') {
      revealInTree(node.key)
    } else if (action === 'new-entry') {
      beginCreation(node, 'entry')
    } else if (action === 'new-folder') {
      beginCreation(node, 'folder')
    } else if (action === 'rename-entry' || action === 'rename-folder') {
      beginRename(node)
    } else if (action === 'copy-entry-id' && node.entryId) {
      try {
        await navigator.clipboard.writeText(node.entryId)
        messageApi.success('Entry ID copied.')
      } catch {
        messageApi.error('Entry ID could not be copied.')
      }
    } else if (action === 'delete-entry' || action === 'delete-folder') {
      const keys = selectedKeys.includes(node.key) ? selectedKeys : [node.key]
      requestDeletion(keys)
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

  return (
    <Flex
      vertical
      gap={layoutTokens.spacing.small}
      style={{ height: '100%', minHeight: 0 }}
      onMouseDown={handlePanelMouseDown}
    >
      {messageContext}
      {modalContext}
      <Input.Search
        allowClear
        value={query}
        placeholder="Search paths, IDs, and localized texts"
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
      <div ref={treeContainerRef} style={{ flex: 1, minHeight: 0, overflow: 'auto' }}>
        <Tree<CatalogTreeNode>
          blockNode
          multiple
          motion={null}
          autoExpandParent={Boolean(query.trim())}
          expandedKeys={visibleExpandedKeys}
          selectedKeys={selectedKeys}
          treeData={nodes}
          draggable={{
            icon: false,
            nodeDraggable: (node) => {
              const kind = (node as CatalogTreeNode).kind
              return !query.trim() && (kind === 'folder' || kind === 'entry')
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
              revealColor={token.colorWarningBg}
              rowHeight={token.controlHeightSM}
              isRevealed={revealedKey === node.key}
              nodeDraft={nodeDraft}
              nodeRenameDraft={nodeRenameDraft}
              canReveal={Boolean(query.trim())}
              onDraftChange={(name) => setNodeDraft((draft) =>
                draft ? { ...draft, name, error: undefined } : draft)}
              onDraftSubmit={submitNodeDraft}
              onDraftCancel={() => setNodeDraft(undefined)}
              onRenameChange={(name) => setNodeRenameDraft((draft) =>
                draft ? { ...draft, name, error: undefined } : draft)}
              onRenameSubmit={submitNodeRename}
              onRenameCancel={() => setNodeRenameDraft(undefined)}
              onAction={handleAction}
            />
          )}
          onExpand={(_, info) => {
            if (!query.trim()) {
              onExpandedKeysChange(info.expanded
                ? mergeKeys(expandedKeys, [info.node.key])
                : expandedKeys.filter((key) => key !== info.node.key))
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
            if (!query.trim()) {
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
        />
      </div>
    </Flex>
  )
}

function describeDeletion(tree: CatalogTreeModel, keys: Key[]) {
  if (keys.length !== 1) {
    return {
      title: `Delete ${keys.length} selected items?`,
      content: 'Folders and all entries contained in them will be removed from the working copy.',
    }
  }

  const item = tree.selectionByKey.get(String(keys[0]))!
  if (item.kind === 'entry') {
    return {
      title: `Delete '${getLastPathSegment(item.entry.path)}'?`,
      content: 'The entry will be removed from the working copy.',
    }
  }

  if (item.kind !== 'folder') {
    throw new Error('Only entries and folders can be deleted from the catalog tree.')
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

function TreeNodeTitle({
  node,
  iconColor,
  dirtyColor,
  revealColor,
  rowHeight,
  isRevealed,
  nodeDraft,
  nodeRenameDraft,
  canReveal,
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
  revealColor: string
  rowHeight: number
  isRevealed: boolean
  nodeDraft?: NodeDraft
  nodeRenameDraft?: NodeRenameDraft
  canReveal: boolean
  onDraftChange: (name: string) => void
  onDraftSubmit: () => void
  onDraftCancel: () => void
  onRenameChange: (name: string) => void
  onRenameSubmit: () => void
  onRenameCancel: () => void
  onAction: (node: CatalogTreeNode, action: string) => void
}) {
  const { token } = theme.useToken()
  const [isHovered, setIsHovered] = useState(false)
  const [isContextMenuOpen, setIsContextMenuOpen] = useState(false)
  const quickActions = getQuickActions(node.kind)

  useEffect(() => {
    if (!isContextMenuOpen) {
      return
    }

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsContextMenuOpen(false)
      }
    }

    window.addEventListener('keydown', handleEscape)
    return () => window.removeEventListener('keydown', handleEscape)
  }, [isContextMenuOpen])

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
    <>
      {isContextMenuOpen && createPortal(
        <div
          aria-hidden
          style={{
            position: 'fixed',
            inset: 0,
            zIndex: token.zIndexPopupBase - 1,
          }}
          onMouseDown={(event) => {
            event.preventDefault()
            event.stopPropagation()
          }}
          onClick={(event) => {
            event.preventDefault()
            event.stopPropagation()
            setIsContextMenuOpen(false)
          }}
          onContextMenu={(event) => {
            event.preventDefault()
            event.stopPropagation()
            setIsContextMenuOpen(false)
          }}
        />,
        document.body,
      )}
      <Dropdown
        open={isContextMenuOpen}
        onOpenChange={(open) => {
          if (open) {
            setIsContextMenuOpen(true)
          }
        }}
        menu={{
          items: getContextMenuItems(node, canReveal),
          onClick: ({ key, domEvent }) => {
            domEvent.stopPropagation()
            setIsContextMenuOpen(false)
            onAction(node, key)
          },
        }}
        trigger={['contextMenu']}
      >
      <span
        style={{
          display: 'inline-flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: layoutTokens.spacing.small,
          width: '100%',
          height: rowHeight,
          minWidth: 0,
          backgroundColor: isRevealed ? revealColor : 'transparent',
          borderRadius: token.borderRadiusSM,
          transition: 'background-color 600ms ease-out',
        }}
        onMouseEnter={() => setIsHovered(true)}
        onMouseLeave={() => setIsHovered(false)}
      >
        <span
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: layoutTokens.spacing.small,
            minWidth: 0,
          }}
        >
          {renderCatalogNodeIcon(node.kind, iconColor)}
          <Typography.Text
            type={node.isTemporary ? 'secondary' : undefined}
            style={{
              color: node.isDirty ? dirtyColor : undefined,
              overflow: 'hidden',
              textOverflow: 'ellipsis',
            }}
          >
            {node.title}
          </Typography.Text>
        </span>
        {isHovered && quickActions.length > 0 ? (
          <span
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
        ) : node.count !== undefined ? (
          <Typography.Text type="secondary" style={{ flex: '0 0 auto', fontSize: 12 }}>
            {node.count}
          </Typography.Text>
        ) : null}
      </span>
      </Dropdown>
    </>
  )
}

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
        { key: 'rename-locale', icon: <EditOutlined />, label: 'Rename' },
        { type: 'divider' },
        { key: 'delete-locale', icon: <DeleteOutlined />, label: 'Delete', danger: true },
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
