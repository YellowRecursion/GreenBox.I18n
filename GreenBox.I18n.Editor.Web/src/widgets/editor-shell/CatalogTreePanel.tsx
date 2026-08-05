import { useEffect, useMemo, useRef, useState, type Key, type ReactNode } from 'react'
import {
  CopyOutlined,
  DeleteOutlined,
  EditOutlined,
  FileAddOutlined,
  FileTextOutlined,
  FolderAddOutlined,
  FolderOutlined,
  GlobalOutlined,
  PlusOutlined,
  TranslationOutlined,
} from '@ant-design/icons'
import {
  Button,
  Dropdown,
  Flex,
  Input,
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
}

interface NodeDraft {
  kind: 'entry' | 'folder'
  parentKey: Key
  parentPath: string
  name: string
  isSaving: boolean
  error?: string
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
}: CatalogTreePanelProps) {
  const { token } = theme.useToken()
  const [messageApi, messageContext] = message.useMessage()
  const [query, setQuery] = useState('')
  const [selectionAnchor, setSelectionAnchor] = useState<Key>()
  const [nodeDraft, setNodeDraft] = useState<NodeDraft>()
  const filteredNodes = useMemo(() => filterCatalogTree(tree.nodes, query), [tree.nodes, query])
  const nodes = useMemo(
    () => nodeDraft ? insertNodeDraft(filteredNodes, nodeDraft) : filteredNodes,
    [filteredNodes, nodeDraft],
  )
  const visibleExpandedKeys = query.trim()
    ? collectExpandableKeys(nodes)
    : expandedKeys
  const visibleSelectionKeys = collectVisibleSelectionKeys(nodes, new Set(visibleExpandedKeys))

  const beginCreation = (node: CatalogTreeNode, kind: NodeDraft['kind']) => {
    if (node.kind !== 'entries-root' && node.kind !== 'folder') {
      return
    }

    setQuery('')
    onExpandedKeysChange(mergeKeys(expandedKeys, [node.key]))
    setNodeDraft({
      kind,
      parentKey: node.key,
      parentPath: node.path ?? '',
      name: kind === 'entry' ? 'NewEntry' : 'NewFolder',
      isSaving: false,
    })
  }

  const handleAction = async (node: CatalogTreeNode, action: string) => {
    if (action === 'new-entry') {
      beginCreation(node, 'entry')
    } else if (action === 'new-folder') {
      beginCreation(node, 'folder')
    } else if (action === 'delete-entry' || action === 'delete-folder') {
      const keys = selectedKeys.includes(node.key) ? selectedKeys : [node.key]
      try {
        await onRemoveNodes(keys)
      } catch (error: unknown) {
        messageApi.error(error instanceof Error ? error.message : 'Selection could not be deleted.')
      }
    }
  }

  const submitNodeDraft = async () => {
    if (!nodeDraft || nodeDraft.isSaving) {
      return
    }

    const name = nodeDraft.name.trim()
    if (!name) {
      setNodeDraft({ ...nodeDraft, error: `${capitalize(nodeDraft.kind)} name is required.` })
      return
    }

    if (!pathSegmentPattern.test(name)) {
      setNodeDraft({
        ...nodeDraft,
        error: 'The name must start with a Latin letter or underscore and contain only letters, digits, and underscores.',
      })
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

  return (
    <Flex vertical gap={layoutTokens.spacing.small} style={{ height: '100%', minHeight: 0 }}>
      {messageContext}
      <Input.Search
        allowClear
        value={query}
        placeholder="Search paths, IDs, and localized texts"
        onChange={(event) => setQuery(event.target.value)}
      />
      <div style={{ flex: 1, minHeight: 0, overflow: 'auto' }}>
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
              iconColor={getIconColor((node as CatalogTreeNode).kind, token)}
              rowHeight={token.controlHeightSM}
              nodeDraft={nodeDraft}
              onDraftChange={(name) => setNodeDraft((draft) =>
                draft ? { ...draft, name, error: undefined } : draft)}
              onDraftSubmit={submitNodeDraft}
              onDraftCancel={() => setNodeDraft(undefined)}
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

            onSelectionChange([key])
          }}
        />
      </div>
    </Flex>
  )
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
  rowHeight,
  nodeDraft,
  onDraftChange,
  onDraftSubmit,
  onDraftCancel,
  onAction,
}: {
  node: CatalogTreeNode
  iconColor: string
  rowHeight: number
  nodeDraft?: NodeDraft
  onDraftChange: (name: string) => void
  onDraftSubmit: () => void
  onDraftCancel: () => void
  onAction: (node: CatalogTreeNode, action: string) => void
}) {
  const [isHovered, setIsHovered] = useState(false)
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

  return (
    <Dropdown
      menu={{
        items: getContextMenuItems(node),
        onClick: ({ key }) => {
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
          {treeIcon(node.kind, iconColor)}
          <Typography.Text
            type={node.isTemporary ? 'secondary' : undefined}
            style={{ overflow: 'hidden', textOverflow: 'ellipsis' }}
          >
            {node.title}{node.isDirty ? ' *' : ''}
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
  draft: NodeDraft
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
        {treeIcon(draft.kind === 'entry' ? 'entry-draft' : 'folder-draft', iconColor)}
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

function getContextMenuItems(node: CatalogTreeNode): MenuProps['items'] {
  switch (node.kind) {
    case 'locales-root':
      return [
        { key: 'new-locale', icon: <PlusOutlined />, label: 'New locale' },
      ]
    case 'locale':
      return [
        { key: 'rename-locale', icon: <EditOutlined />, label: 'Rename' },
        { type: 'divider' },
        { key: 'delete-locale', icon: <DeleteOutlined />, label: 'Delete', danger: true },
      ]
    case 'entries-root':
      return createContainerMenuItems(false)
    case 'folder':
      return createContainerMenuItems(true)
    case 'entry':
      return [
        { key: 'rename-entry', icon: <EditOutlined />, label: 'Rename' },
        { key: 'duplicate-entry', icon: <CopyOutlined />, label: 'Duplicate' },
        { type: 'divider' },
        { key: 'delete-entry', icon: <DeleteOutlined />, label: 'Delete', danger: true },
      ]
    case 'entry-draft':
    case 'folder-draft':
      return []
  }
}

function createContainerMenuItems(
  includeOwnActions: boolean,
): MenuProps['items'] {
  return [
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
            danger: true,
          },
        ]
      : []),
  ]
}

function collectExpandableKeys(nodes: CatalogTreeNode[]): Key[] {
  return nodes.flatMap((node) => [
    ...(node.children?.length ? [node.key] : []),
    ...collectExpandableKeys(node.children ?? []),
  ])
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

function getIconColor(
  kind: CatalogTreeNode['kind'],
  token: ReturnType<typeof theme.useToken>['token'],
) {
  switch (kind) {
    case 'locales-root':
    case 'locale':
      return token.colorInfo
    case 'entries-root':
      return token.colorPrimary
    case 'folder':
    case 'folder-draft':
      return token.colorWarning
    case 'entry':
    case 'entry-draft':
      return '#CD4945'
  }
}

function treeIcon(kind: CatalogTreeNode['kind'], color: string): ReactNode {
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
