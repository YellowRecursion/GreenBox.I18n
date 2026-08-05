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
}

interface EntryDraft {
  parentKey: Key
  parentPath: string
  name: string
  isSaving: boolean
  error?: string
}

const entryDraftKey = 'draft:entry'

export function CatalogTreePanel({
  tree,
  selectedKeys,
  expandedKeys,
  onSelectionChange,
  onExpandedKeysChange,
  onAddEntry,
}: CatalogTreePanelProps) {
  const { token } = theme.useToken()
  const [query, setQuery] = useState('')
  const [selectionAnchor, setSelectionAnchor] = useState<Key>()
  const [entryDraft, setEntryDraft] = useState<EntryDraft>()
  const filteredNodes = useMemo(() => filterCatalogTree(tree.nodes, query), [tree.nodes, query])
  const nodes = useMemo(
    () => entryDraft ? insertEntryDraft(filteredNodes, entryDraft) : filteredNodes,
    [filteredNodes, entryDraft],
  )
  const visibleExpandedKeys = query.trim()
    ? collectExpandableKeys(nodes)
    : expandedKeys
  const visibleSelectionKeys = collectVisibleSelectionKeys(nodes, new Set(visibleExpandedKeys))

  const beginEntryCreation = (node: CatalogTreeNode) => {
    if (node.kind !== 'entries-root' && node.kind !== 'folder') {
      return
    }

    setQuery('')
    onExpandedKeysChange(mergeKeys(expandedKeys, [node.key]))
    setEntryDraft({
      parentKey: node.key,
      parentPath: node.path ?? '',
      name: 'NewEntry',
      isSaving: false,
    })
  }

  const handleAction = (node: CatalogTreeNode, action: string) => {
    if (action === 'new-entry') {
      beginEntryCreation(node)
    }
  }

  const submitEntryDraft = async () => {
    if (!entryDraft || entryDraft.isSaving) {
      return
    }

    const name = entryDraft.name.trim()
    if (!name) {
      setEntryDraft({ ...entryDraft, error: 'Entry name is required.' })
      return
    }

    const path = entryDraft.parentPath ? `${entryDraft.parentPath}.${name}` : name
    setEntryDraft({ ...entryDraft, name, isSaving: true, error: undefined })

    try {
      const createdKey = await onAddEntry(path)
      setEntryDraft(undefined)
      onSelectionChange([createdKey])
    } catch (error: unknown) {
      setEntryDraft({
        ...entryDraft,
        name,
        isSaving: false,
        error: error instanceof Error ? error.message : 'Entry could not be created.',
      })
    }
  }

  return (
    <Flex vertical gap={layoutTokens.spacing.small} style={{ height: '100%', minHeight: 0 }}>
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
          autoExpandParent={Boolean(query.trim())}
          expandedKeys={visibleExpandedKeys}
          selectedKeys={selectedKeys}
          treeData={nodes}
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
              entryDraft={entryDraft}
              onDraftChange={(name) => setEntryDraft((draft) =>
                draft ? { ...draft, name, error: undefined } : draft)}
              onDraftSubmit={submitEntryDraft}
              onDraftCancel={() => setEntryDraft(undefined)}
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

function TreeNodeTitle({
  node,
  iconColor,
  rowHeight,
  entryDraft,
  onDraftChange,
  onDraftSubmit,
  onDraftCancel,
  onAction,
}: {
  node: CatalogTreeNode
  iconColor: string
  rowHeight: number
  entryDraft?: EntryDraft
  onDraftChange: (name: string) => void
  onDraftSubmit: () => void
  onDraftCancel: () => void
  onAction: (node: CatalogTreeNode, action: string) => void
}) {
  const [isHovered, setIsHovered] = useState(false)
  const quickActions = getQuickActions(node.kind)

  if (node.kind === 'entry-draft' && entryDraft) {
    return (
      <DraftEntryTitle
        draft={entryDraft}
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
        items: getContextMenuItems(node.kind),
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
          <span style={{ overflow: 'hidden', textOverflow: 'ellipsis' }}>
            {node.title}{node.isDirty ? ' *' : ''}
          </span>
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

function DraftEntryTitle({
  draft,
  iconColor,
  rowHeight,
  onChange,
  onSubmit,
  onCancel,
}: {
  draft: EntryDraft
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
        {treeIcon('entry-draft', iconColor)}
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
      return []
  }
}

function getContextMenuItems(kind: CatalogTreeNode['kind']): MenuProps['items'] {
  switch (kind) {
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
      return []
  }
}

function createContainerMenuItems(includeOwnActions: boolean): MenuProps['items'] {
  return [
    { key: 'new-entry', icon: <FileAddOutlined />, label: 'New entry' },
    { key: 'new-folder', icon: <FolderAddOutlined />, label: 'New folder' },
    ...(includeOwnActions
      ? [
          { type: 'divider' as const },
          { key: 'rename-folder', icon: <EditOutlined />, label: 'Rename' },
          { key: 'delete-folder', icon: <DeleteOutlined />, label: 'Delete', danger: true },
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
      return <FolderOutlined style={style} />
    case 'entry':
    case 'entry-draft':
      return <FileTextOutlined style={style} />
  }
}

function insertEntryDraft(nodes: CatalogTreeNode[], draft: EntryDraft): CatalogTreeNode[] {
  return nodes.map((node) => {
    if (node.key === draft.parentKey) {
      return {
        ...node,
        children: [
          ...(node.children ?? []),
          {
            key: entryDraftKey,
            title: draft.name,
            kind: 'entry-draft',
            searchText: draft.name,
            selectable: false,
          },
        ],
      }
    }

    return node.children?.length
      ? { ...node, children: insertEntryDraft(node.children, draft) }
      : node
  })
}
