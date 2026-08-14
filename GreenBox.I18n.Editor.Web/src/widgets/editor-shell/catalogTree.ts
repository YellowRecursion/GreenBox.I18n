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
  hasWarning?: boolean
  hasUnusedWarning?: boolean
  warningMessages?: string[]
  entryId?: string
  children?: CatalogTreeNode[]
}

export interface CatalogTreeWarningOptions {
  usageCounts?: ReadonlyMap<string, number>
  warnUnusedEntries: boolean
  warnIncompleteEntries: boolean
}

export type CatalogSelectionItem =
  | { kind: 'locale'; locale: CatalogLocale }
  | { kind: 'folder'; path: string; entryCount: number; folderCount: number }
  | { kind: 'entry'; entry: CatalogEntry }

export interface CatalogTreeModel {
  nodes: CatalogTreeNode[]
  selectionByKey: ReadonlyMap<string, CatalogSelectionItem>
  searchRecords: readonly CatalogTreeSearchRecord[]
  expandableKeysByKey: ReadonlyMap<string, readonly string[]>
}

export interface CatalogTreeSearchRecord {
  key: string
  normalizedText: string
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
  warningOptions: CatalogTreeWarningOptions,
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
    createEntryTreeNode(
      child,
      selectionByKey,
      dirtyEntryIds,
      dirtyPaths,
      catalog.locales,
      warningOptions,
    ))

  const nodes: CatalogTreeNode[] = [
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
      hasWarning: entryNodes.some((node) => node.hasWarning),
      path: '',
      isDirty: dirtyPaths.size > 0 || dirtyEntryIds.size > 0 || temporaryFolderPaths.length > 0,
      selectable: false,
      children: entryNodes,
    },
  ]

  return {
    nodes,
    selectionByKey,
    searchRecords: collectSearchRecords(nodes),
    expandableKeysByKey: createExpandableKeyIndex(nodes),
  }
}

export function filterCatalogTree(
  nodes: CatalogTreeNode[],
  matchingKeys: ReadonlySet<string>,
): CatalogTreeNode[] {
  return nodes
    .map((node) => filterNode(node, matchingKeys))
    .filter((node): node is CatalogTreeNode => node != null)
}

export function findMatchingCatalogKeys(
  records: readonly CatalogTreeSearchRecord[],
  query: string,
): ReadonlySet<string> | undefined {
  const normalizedQuery = query.trim().toLowerCase()
  if (!normalizedQuery) {
    return undefined
  }

  return new Set(records
    .filter((record) => record.normalizedText.includes(normalizedQuery))
    .map((record) => record.key))
}

function filterNode(
  node: CatalogTreeNode,
  matchingKeys: ReadonlySet<string>,
): CatalogTreeNode | null {
  const children = node.children
    ?.map((child) => filterNode(child, matchingKeys))
    .filter((child): child is CatalogTreeNode => child != null)

  if (!matchingKeys.has(node.key) && !children?.length) {
    return null
  }

  return children?.length ? { ...node, children } : { ...node, children: undefined }
}

function collectSearchRecords(nodes: readonly CatalogTreeNode[]): CatalogTreeSearchRecord[] {
  return nodes.flatMap((node) => [
    ...(node.kind === 'locales-root' || node.kind === 'entries-root'
      ? []
      : [{ key: node.key, normalizedText: node.searchText.toLowerCase() }]),
    ...collectSearchRecords(node.children ?? []),
  ])
}

function createExpandableKeyIndex(
  nodes: readonly CatalogTreeNode[],
): ReadonlyMap<string, readonly string[]> {
  const index = new Map<string, readonly string[]>()

  const visit = (node: CatalogTreeNode): string[] => {
    if (!node.children?.length) {
      return []
    }

    const branchKeys = [node.key]
    for (const child of node.children) {
      branchKeys.push(...visit(child))
    }

    index.set(node.key, branchKeys)
    return branchKeys
  }

  for (const node of nodes) {
    visit(node)
  }

  return index
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
  locales: readonly CatalogLocale[],
  warningOptions: CatalogTreeWarningOptions,
): CatalogTreeNode {
  if ('children' in child) {
    const key = folderKey(child.path)
    const children = child.children.map((nestedChild) =>
      createEntryTreeNode(
        nestedChild,
        selectionByKey,
        dirtyEntryIds,
        dirtyPaths,
        locales,
        warningOptions,
      ))
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
      hasWarning: children.some((node) => node.hasWarning),
      path: child.path,
      isDirty:
        child.isTemporary ||
        hasDirtyPath(child.path, dirtyPaths) ||
        hasDirtyEntry(child, dirtyEntryIds),
      isTemporary: child.isTemporary,
      children,
    }
  }

  const usageCount = warningOptions.usageCounts?.get(child.id) ?? 0
  const hasUnusedWarning = warningOptions.warnUnusedEntries &&
    warningOptions.usageCounts !== undefined &&
    usageCount === 0
  const warningMessages = [
    ...(hasUnusedWarning ? ['No usages found'] : []),
    ...(warningOptions.warnIncompleteEntries
      ? getIncompleteLocalizationWarnings(child, locales)
      : []),
  ]

  return {
    key: entryKey(child.id),
    title: getEntryTitle(child),
    kind: 'entry',
    path: child.path,
    entryId: child.id,
    count: warningOptions.usageCounts === undefined ? undefined : usageCount,
    hasWarning: warningMessages.length > 0,
    hasUnusedWarning,
    warningMessages: warningMessages.length > 0 ? warningMessages : undefined,
    isDirty: dirtyEntryIds.has(child.id) || dirtyPaths.has(child.path),
    searchText: [
      child.id,
      child.path,
      ...Object.values(child.locales).map((value) => value.text ?? ''),
    ].join(' '),
  }
}

function getIncompleteLocalizationWarnings(
  entry: CatalogEntry,
  locales: readonly CatalogLocale[],
) {
  const usesText = locales.some((locale) => hasText(entry.locales[locale.id]?.text))
  const usesAsset = locales.some((locale) => entry.locales[locale.id]?.asset != null)
  if (!usesText && !usesAsset) {
    return ['No localized content']
  }

  const warnings: string[] = []
  if (usesText) {
    const missingText = locales.filter((locale) => !hasText(entry.locales[locale.id]?.text))
    if (missingText.length > 0) {
      warnings.push(`Missing text: ${formatLocaleNames(missingText)}`)
    }
  }

  if (usesAsset) {
    const missingAssets = locales.filter((locale) => entry.locales[locale.id]?.asset == null)
    if (missingAssets.length > 0) {
      warnings.push(`Missing asset: ${formatLocaleNames(missingAssets)}`)
    }
  }

  return warnings
}

function hasText(text: string | null | undefined) {
  return Boolean(text?.trim())
}

function formatLocaleNames(locales: readonly CatalogLocale[]) {
  const visible = locales.slice(0, 3).map((locale) => locale.displayName)
  const remaining = locales.length - visible.length
  return remaining > 0
    ? `${visible.join(', ')}, and ${remaining} more`
    : visible.join(', ')
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
