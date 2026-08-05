import type { CatalogEntry, CatalogLocale, CatalogSnapshot } from '../../entities/catalog/model/catalog'

export type CatalogTreeNodeKind = 'locales-root' | 'entries-root' | 'locale' | 'folder' | 'entry' | 'entry-draft'

export interface CatalogTreeNode {
  key: string
  title: string
  kind: CatalogTreeNodeKind
  searchText: string
  count?: number
  selectable?: boolean
  path?: string
  isDirty?: boolean
  children?: CatalogTreeNode[]
}

export type CatalogSelectionItem =
  | { kind: 'locale'; locale: CatalogLocale }
  | { kind: 'folder'; path: string; entryCount: number }
  | { kind: 'entry'; entry: CatalogEntry }

export interface CatalogTreeModel {
  nodes: CatalogTreeNode[]
  selectionByKey: ReadonlyMap<string, CatalogSelectionItem>
}

interface FolderBuilder {
  path: string
  title: string
  entryCount: number
  children: Array<FolderBuilder | CatalogEntry>
  folders: Map<string, FolderBuilder>
}

export function buildCatalogTree(catalog: CatalogSnapshot): CatalogTreeModel {
  const selectionByKey = new Map<string, CatalogSelectionItem>()
  const dirtyEntryIds = new Set(catalog.dirtyEntryIds)
  const localeNodes = catalog.locales.map((locale) => {
    const key = localeKey(locale.id)
    selectionByKey.set(key, { kind: 'locale', locale })

    return {
      key,
      title: locale.displayName,
      kind: 'locale' as const,
      searchText: `${locale.id} ${locale.displayName} ${locale.culture}`,
    }
  })

  const rootFolder = createFolder('', '')
  for (const entry of catalog.entries) {
    addEntry(rootFolder, entry)
    selectionByKey.set(entryKey(entry.id), { kind: 'entry', entry })
  }

  const entryNodes = rootFolder.children.map((child) =>
    createEntryTreeNode(child, selectionByKey, dirtyEntryIds))

  return {
    nodes: [
      {
        key: 'root:locales',
        title: 'Locales',
        kind: 'locales-root',
        searchText: 'locales',
        count: catalog.locales.length,
        selectable: false,
        children: localeNodes,
      },
      {
        key: 'root:entries',
        title: 'Entries',
        kind: 'entries-root',
        searchText: 'entries',
        count: catalog.entries.length,
        path: '',
        isDirty: dirtyEntryIds.size > 0,
        selectable: false,
        children: entryNodes,
      },
    ],
    selectionByKey,
  }
}

export function filterCatalogTree(nodes: CatalogTreeNode[], query: string): CatalogTreeNode[] {
  const normalizedQuery = query.trim().toLocaleLowerCase()
  if (!normalizedQuery) {
    return nodes
  }

  return nodes
    .map((node) => filterNode(node, normalizedQuery))
    .filter((node): node is CatalogTreeNode => node != null)
}

function filterNode(node: CatalogTreeNode, query: string): CatalogTreeNode | null {
  if (node.searchText.toLocaleLowerCase().includes(query)) {
    return node
  }

  const children = node.children
    ?.map((child) => filterNode(child, query))
    .filter((child): child is CatalogTreeNode => child != null)

  return children?.length ? { ...node, children } : null
}

function createFolder(path: string, title: string): FolderBuilder {
  return { path, title, entryCount: 0, children: [], folders: new Map() }
}

function addEntry(root: FolderBuilder, entry: CatalogEntry) {
  const segments = entry.path.split('.')
  let folder = root

  for (let index = 0; index < segments.length - 1; index++) {
    const title = segments[index]
    const path = folder.path ? `${folder.path}.${title}` : title
    let childFolder = folder.folders.get(title)

    if (!childFolder) {
      childFolder = createFolder(path, title)
      folder.folders.set(title, childFolder)
      folder.children.push(childFolder)
    }

    childFolder.entryCount++
    folder = childFolder
  }

  folder.children.push(entry)
}

function createEntryTreeNode(
  child: FolderBuilder | CatalogEntry,
  selectionByKey: Map<string, CatalogSelectionItem>,
  dirtyEntryIds: ReadonlySet<string>,
): CatalogTreeNode {
  if ('children' in child) {
    const key = folderKey(child.path)
    selectionByKey.set(key, { kind: 'folder', path: child.path, entryCount: child.entryCount })

    return {
      key,
      title: child.title,
      kind: 'folder',
      searchText: child.path,
      count: child.entryCount,
      path: child.path,
      isDirty: hasDirtyEntry(child, dirtyEntryIds),
      children: child.children.map((nestedChild) =>
        createEntryTreeNode(nestedChild, selectionByKey, dirtyEntryIds)),
    }
  }

  return {
    key: entryKey(child.id),
    title: child.path.split('.').at(-1) ?? child.path,
    kind: 'entry',
    path: child.path,
    isDirty: dirtyEntryIds.has(child.id),
    searchText: [
      child.id,
      child.path,
      ...Object.values(child.locales).map((value) => value.text ?? ''),
    ].join(' '),
  }
}

function hasDirtyEntry(folder: FolderBuilder, dirtyEntryIds: ReadonlySet<string>): boolean {
  return folder.children.some((child) =>
    'children' in child ? hasDirtyEntry(child, dirtyEntryIds) : dirtyEntryIds.has(child.id))
}

function localeKey(id: string) {
  return `locale:${id}`
}

function folderKey(path: string) {
  return `folder:${path}`
}

function entryKey(id: string) {
  return `entry:${id}`
}
