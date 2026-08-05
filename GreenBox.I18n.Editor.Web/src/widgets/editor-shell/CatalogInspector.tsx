import type { ReactNode } from 'react'
import { Card, Descriptions, Empty, Flex, Tag, Typography } from 'antd'
import { layoutTokens } from '../../design/layoutTokens'
import type { CatalogAssetReference, CatalogEntry, CatalogLocale } from '../../entities/catalog/model/catalog'
import type { CatalogSelectionItem } from './catalogTree'

interface CatalogInspectorProps {
  selection: CatalogSelectionItem[]
  defaultLocale: string
  locales: CatalogLocale[]
}

export function CatalogInspector({ selection, defaultLocale, locales }: CatalogInspectorProps) {
  if (selection.length === 0) {
    return <Empty description="Select a locale, folder, or entry" />
  }

  if (selection.length > 1) {
    return <MultipleSelectionInspector selection={selection} />
  }

  const item = selection[0]
  switch (item.kind) {
    case 'locale':
      return <LocaleInspector locale={item.locale} defaultLocale={defaultLocale} />
    case 'folder':
      return <FolderInspector path={item.path} entryCount={item.entryCount} />
    case 'entry':
      return <EntryInspector entry={item.entry} defaultLocale={defaultLocale} locales={locales} />
  }
}

function LocaleInspector({ locale, defaultLocale }: { locale: CatalogLocale; defaultLocale: string }) {
  return (
    <InspectorSection title={locale.displayName} type="Locale">
      <Descriptions
        bordered
        column={1}
        size="small"
        items={[
          { key: 'id', label: 'ID', children: locale.id },
          { key: 'culture', label: 'Culture', children: locale.culture },
          { key: 'fallback', label: 'Fallback', children: locale.fallback ?? '[none]' },
          { key: 'default', label: 'Default', children: locale.id === defaultLocale ? 'Yes' : 'No' },
          { key: 'icon', label: 'Icon', children: <AssetReference asset={locale.icon} /> },
        ]}
      />
    </InspectorSection>
  )
}

function FolderInspector({ path, entryCount }: { path: string; entryCount: number }) {
  return (
    <InspectorSection title={path.split('.').at(-1) ?? path} type="Folder">
      <Descriptions
        bordered
        column={1}
        size="small"
        items={[
          { key: 'path', label: 'Path', children: path },
          { key: 'entries', label: 'Entries', children: entryCount },
        ]}
      />
    </InspectorSection>
  )
}

function EntryInspector({
  entry,
  defaultLocale,
  locales,
}: {
  entry: CatalogEntry
  defaultLocale: string
  locales: CatalogLocale[]
}) {
  return (
    <InspectorSection title={entry.path.split('.').at(-1) ?? entry.path} type="Entry">
      <Descriptions
        bordered
        column={1}
        size="small"
        items={[
          { key: 'id', label: 'ID', children: entry.id },
          { key: 'path', label: 'Path', children: entry.path },
          { key: 'comment', label: 'Comment', children: entry.comment ?? '[none]' },
        ]}
      />
      <Flex vertical gap={layoutTokens.spacing.small}>
        {locales.map((locale) => {
          const value = entry.locales[locale.id]
          return (
          <Card
            key={locale.id}
            size="small"
            title={`${locale.displayName} (${locale.id})`}
            extra={locale.id === defaultLocale ? <Tag color="blue">Default</Tag> : undefined}
          >
            <Descriptions
              column={1}
              size="small"
              items={[
                {
                  key: 'text',
                  label: 'Text',
                  children: <Typography.Text style={{ whiteSpace: 'pre-wrap' }}>{value?.text ?? '[none]'}</Typography.Text>,
                },
                { key: 'asset', label: 'Asset', children: <AssetReference asset={value?.asset ?? null} /> },
              ]}
            />
          </Card>
          )
        })}
      </Flex>
    </InspectorSection>
  )
}

function MultipleSelectionInspector({ selection }: { selection: CatalogSelectionItem[] }) {
  const localeCount = selection.filter((item) => item.kind === 'locale').length
  const folderCount = selection.filter((item) => item.kind === 'folder').length
  const entryCount = selection.filter((item) => item.kind === 'entry').length

  return (
    <InspectorSection title={`${selection.length} items selected`} type="Multiple selection">
      <Descriptions
        bordered
        column={1}
        size="small"
        items={[
          { key: 'locales', label: 'Locales', children: localeCount },
          { key: 'folders', label: 'Folders', children: folderCount },
          { key: 'entries', label: 'Entries', children: entryCount },
        ]}
      />
    </InspectorSection>
  )
}

function InspectorSection({ title, type, children }: { title: string; type: string; children: ReactNode }) {
  return (
    <Flex vertical gap={layoutTokens.spacing.large} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={4} style={{ margin: 0 }}>{title}</Typography.Title>
        <Typography.Text type="secondary">{type}</Typography.Text>
      </div>
      {children}
    </Flex>
  )
}

function AssetReference({ asset }: { asset: CatalogAssetReference | null }) {
  if (!asset) {
    return '[none]'
  }

  return asset.localFileId
    ? `${asset.assetGuid} : ${asset.localFileId}`
    : asset.assetGuid
}
