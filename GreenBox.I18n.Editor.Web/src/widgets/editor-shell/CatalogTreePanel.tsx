import { useMemo, useState, type Key, type ReactNode } from 'react'
import { FileTextOutlined, FolderOutlined, GlobalOutlined, TranslationOutlined } from '@ant-design/icons'
import { Flex, Input, Tree, Typography, theme } from 'antd'
import { layoutTokens } from '../../design/layoutTokens'
import { filterCatalogTree, type CatalogTreeModel, type CatalogTreeNode } from './catalogTree'

interface CatalogTreePanelProps {
  tree: CatalogTreeModel
  selectedKeys: Key[]
  onSelectionChange: (keys: Key[]) => void
}

export function CatalogTreePanel({ tree, selectedKeys, onSelectionChange }: CatalogTreePanelProps) {
  const { token } = theme.useToken()
  const [query, setQuery] = useState('')
  const [expandedKeys, setExpandedKeys] = useState<Key[]>(['root:locales', 'root:entries'])
  const [selectionAnchor, setSelectionAnchor] = useState<Key>()
  const nodes = useMemo(() => filterCatalogTree(tree.nodes, query), [tree.nodes, query])
  const visibleExpandedKeys = query.trim()
    ? collectExpandableKeys(nodes)
    : expandedKeys
  const visibleSelectionKeys = collectVisibleSelectionKeys(nodes, new Set(visibleExpandedKeys))

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
            itemTitle: { display: 'inline-block', width: '100%', minWidth: 0 },
          }}
          titleRender={(node) => (
            <TreeNodeTitle
              node={node as CatalogTreeNode}
              iconColor={getIconColor((node as CatalogTreeNode).kind, token)}
            />
          )}
          onExpand={(keys) => {
            if (!query.trim()) {
              setExpandedKeys(keys)
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

function TreeNodeTitle({ node, iconColor }: { node: CatalogTreeNode; iconColor: string }) {
  return (
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: layoutTokens.spacing.small,
        width: '100%',
        minWidth: 0,
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
        {treeIcon(node.kind, iconColor)}
        <span style={{ overflow: 'hidden', textOverflow: 'ellipsis' }}>{node.title}</span>
      </span>
      {node.count !== undefined && (
        <Typography.Text type="secondary" style={{ flex: '0 0 auto', fontSize: 12 }}>
          {node.count}
        </Typography.Text>
      )}
    </span>
  )
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
      return <FileTextOutlined style={style} />
  }
}
