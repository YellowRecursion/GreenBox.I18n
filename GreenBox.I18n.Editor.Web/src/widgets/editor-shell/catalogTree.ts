import type { CatalogEntry, CatalogLocale, CatalogSnapshot } from '../../entities/catalog/model/catalog'

export type CatalogTreeNodeKind =
  | 'locales-root'
  | 'entries-root'
  | 'locale'
  | 'folder'
  | 'entry'
  | 'entry-draft'
  | 'folder-draft'

export interface CatalogTreeNode {
  key: string
  title: string
  kind: CatalogTreeNodeKind
  searchText: string
  count?: number
  selectable?: boolean
  path?: string
  culture?: string
  isDirty?: boolean
  isTemporary?: boolean
  entryId?: string
  children?: CatalogTreeNode[]
}

export type CatalogSelectionItem =
  | { kind: 'locale'; locale: CatalogLocale }
  | { kind: 'folder'; path: string; entryCount: number; folderCount: number }
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
  isTemporary: boolean
}

export function buildCatalogTree(
  catalog: CatalogSnapshot,
  temporaryFolderPaths: readonly string[] = [],
): CatalogTreeModel {
  const selectionByKey = new Map<string, CatalogSelectionItem>()
  const dirtyLocaleIds = new Set(catalog.dirtyLocaleIds ?? [])
  const dirtyEntryIds = new Set(catalog.dirtyEntryIds)
  const dirtyPaths = new Set(catalog.dirtyPaths ?? [])
  const localeNodes = catalog.locales.map((locale) => {
    const key = localeKey(locale.id)
    selectionByKey.set(key, { kind: 'locale', locale })

    return {
      key,
      title: locale.displayName,
      kind: 'locale' as const,
      culture: locale.culture,
      isDirty: dirtyLocaleIds.has(locale.id),
      searchText: `${locale.id} ${locale.displayName} ${locale.culture}`,
    }
  })

  const rootFolder = createFolder('', '')
  for (const entry of catalog.entries) {
    addEntry(rootFolder, entry)
    selectionByKey.set(entryKey(entry.id), { kind: 'entry', entry })
  }

  for (const path of temporaryFolderPaths) {
    addTemporaryFolder(rootFolder, path)
  }

  sortFolderChildren(rootFolder)

  const entryNodes = rootFolder.children.map((child) =>
    createEntryTreeNode(child, selectionByKey, dirtyEntryIds, dirtyPaths))

  return {
    nodes: [
      {
        key: 'root:locales',
        title: 'Locales',
        kind: 'locales-root',
        searchText: 'locales',
        count: catalog.locales.length,
        isDirty: dirtyLocaleIds.size > 0,
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
        isDirty: dirtyPaths.size > 0 || dirtyEntryIds.size > 0 || temporaryFolderPaths.length > 0,
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
  return { path, title, entryCount: 0, children: [], folders: new Map(), isTemporary: false }
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

function addTemporaryFolder(root: FolderBuilder, path: string) {
  const segments = path.split('.')
  let folder = root

  for (const title of segments) {
    const childPath = folder.path ? `${folder.path}.${title}` : title
    let childFolder = folder.folders.get(title)

    if (!childFolder) {
      childFolder = createFolder(childPath, title)
      childFolder.isTemporary = true
      folder.folders.set(title, childFolder)
      folder.children.push(childFolder)
    }

    folder = childFolder
  }

  if (folder.entryCount === 0) {
    folder.isTemporary = true
  }
}

function sortFolderChildren(folder: FolderBuilder) {
  folder.children.sort(compareFolderChildren)

  for (const child of folder.children) {
    if ('children' in child) {
      sortFolderChildren(child)
    }
  }
}

function compareFolderChildren(
  left: FolderBuilder | CatalogEntry,
  right: FolderBuilder | CatalogEntry,
) {
  const leftIsFolder = 'children' in left
  const rightIsFolder = 'children' in right
  if (leftIsFolder !== rightIsFolder) {
    return leftIsFolder ? -1 : 1
  }

  const leftTitle = leftIsFolder ? left.title : getEntryTitle(left)
  const rightTitle = rightIsFolder ? right.title : getEntryTitle(right)
  const naturalComparison = compareNaturalIgnoreCase(leftTitle, rightTitle)
  if (naturalComparison !== 0) {
    return naturalComparison
  }

  return compareOrdinal(leftTitle, rightTitle)
}

function compareNaturalIgnoreCase(left: string, right: string) {
  let leftIndex = 0
  let rightIndex = 0

  while (leftIndex < left.length && rightIndex < right.length) {
    const leftIsDigit = isAsciiDigit(left[leftIndex])
    const rightIsDigit = isAsciiDigit(right[rightIndex])

    if (leftIsDigit && rightIsDigit) {
      const comparison = compareNumberRuns(left, leftIndex, right, rightIndex)
      leftIndex = comparison.leftEnd
      rightIndex = comparison.rightEnd
      if (comparison.result !== 0) {
        return comparison.result
      }

      continue
    }

    const leftCharacter = left[leftIndex].toUpperCase()
    const rightCharacter = right[rightIndex].toUpperCase()
    const characterComparison = compareOrdinal(leftCharacter, rightCharacter)
    if (characterComparison !== 0) {
      return characterComparison
    }

    leftIndex++
    rightIndex++
  }

  return (left.length - leftIndex) - (right.length - rightIndex)
}

function compareNumberRuns(left: string, leftStart: number, right: string, rightStart: number) {
  let leftEnd = leftStart
  let rightEnd = rightStart

  while (leftEnd < left.length && isAsciiDigit(left[leftEnd])) {
    leftEnd++
  }

  while (rightEnd < right.length && isAsciiDigit(right[rightEnd])) {
    rightEnd++
  }

  const leftSignificantStart = skipLeadingZeroes(left, leftStart, leftEnd)
  const rightSignificantStart = skipLeadingZeroes(right, rightStart, rightEnd)
  const leftSignificantLength = leftEnd - leftSignificantStart
  const rightSignificantLength = rightEnd - rightSignificantStart

  if (leftSignificantLength !== rightSignificantLength) {
    return {
      result: leftSignificantLength - rightSignificantLength,
      leftEnd,
      rightEnd,
    }
  }

  for (let offset = 0; offset < leftSignificantLength; offset++) {
    const digitComparison = compareOrdinal(
      left[leftSignificantStart + offset],
      right[rightSignificantStart + offset],
    )
    if (digitComparison !== 0) {
      return { result: digitComparison, leftEnd, rightEnd }
    }
  }

  return {
    result: (leftEnd - leftStart) - (rightEnd - rightStart),
    leftEnd,
    rightEnd,
  }
}

function skipLeadingZeroes(value: string, start: number, end: number) {
  let index = start
  while (index < end && value[index] === '0') {
    index++
  }

  return index
}

function isAsciiDigit(value: string) {
  return value >= '0' && value <= '9'
}

function compareOrdinal(left: string, right: string) {
  return left < right ? -1 : left > right ? 1 : 0
}

function getEntryTitle(entry: CatalogEntry) {
  return entry.path.split('.').at(-1) ?? entry.path
}

function createEntryTreeNode(
  child: FolderBuilder | CatalogEntry,
  selectionByKey: Map<string, CatalogSelectionItem>,
  dirtyEntryIds: ReadonlySet<string>,
  dirtyPaths: ReadonlySet<string>,
): CatalogTreeNode {
  if ('children' in child) {
    const key = folderKey(child.path)
    selectionByKey.set(key, {
      kind: 'folder',
      path: child.path,
      entryCount: child.entryCount,
      folderCount: countDescendantFolders(child),
    })

    return {
      key,
      title: child.title,
      kind: 'folder',
      searchText: child.path,
      count: child.entryCount,
      path: child.path,
      isDirty:
        child.isTemporary ||
        hasDirtyPath(child.path, dirtyPaths) ||
        hasDirtyEntry(child, dirtyEntryIds),
      isTemporary: child.isTemporary,
      children: child.children.map((nestedChild) =>
        createEntryTreeNode(nestedChild, selectionByKey, dirtyEntryIds, dirtyPaths)),
    }
  }

  return {
    key: entryKey(child.id),
    title: getEntryTitle(child),
    kind: 'entry',
    path: child.path,
    entryId: child.id,
    isDirty: dirtyEntryIds.has(child.id) || dirtyPaths.has(child.path),
    searchText: [
      child.id,
      child.path,
      ...Object.values(child.locales).map((value) => value.text ?? ''),
    ].join(' '),
  }
}

function countDescendantFolders(folder: FolderBuilder): number {
  return folder.children.reduce((count, child) =>
    count + ('children' in child ? 1 + countDescendantFolders(child) : 0), 0)
}

function hasDirtyPath(folderPath: string, dirtyPaths: ReadonlySet<string>): boolean {
  for (const path of dirtyPaths) {
    if (path === folderPath || path.startsWith(`${folderPath}.`)) {
      return true
    }
  }

  return false
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
