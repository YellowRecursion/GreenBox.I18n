import { useEffect, useLayoutEffect, useRef, useState, type ReactNode } from 'react'
import { CopyOutlined } from '@ant-design/icons'
import {
  Button,
  Card,
  Descriptions,
  Empty,
  Flex,
  Input,
  Tag,
  Tabs,
  Tooltip,
  Typography,
  theme,
  type InputRef,
} from 'antd'
import { layoutTokens } from '../../design/layoutTokens'
import type { CatalogAssetReference, CatalogEntry, CatalogLocale } from '../../entities/catalog/model/catalog'
import { LocaleFlag } from '../../entities/catalog/ui/LocaleFlag'
import type { CatalogSelectionItem } from './catalogTree'
import { getCatalogNodeIconColor, renderCatalogNodeIcon } from './catalogNodeVisuals'
import { EntryAssetInput } from './EntryAssetInput'

interface CatalogInspectorProps {
  selection: CatalogSelectionItem[]
  defaultLocale: string
  locales: CatalogLocale[]
  onEntryPathChange: (id: string, path: string) => Promise<void>
  onEntryCommentChange: (id: string, comment: string | null) => Promise<void>
  onEntryAssetChange: (
    id: string,
    localeId: string,
    asset: CatalogAssetReference | null,
  ) => Promise<void>
}

export function CatalogInspector({
  selection,
  defaultLocale,
  locales,
  onEntryPathChange,
  onEntryCommentChange,
  onEntryAssetChange,
}: CatalogInspectorProps) {
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
      return (
        <EntryInspector
          entry={item.entry}
          defaultLocale={defaultLocale}
          locales={locales}
          onPathChange={onEntryPathChange}
          onCommentChange={onEntryCommentChange}
          onAssetChange={onEntryAssetChange}
        />
      )
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
  onPathChange,
  onCommentChange,
  onAssetChange,
}: {
  entry: CatalogEntry
  defaultLocale: string
  locales: CatalogLocale[]
  onPathChange: (id: string, path: string) => Promise<void>
  onCommentChange: (id: string, comment: string | null) => Promise<void>
  onAssetChange: (
    id: string,
    localeId: string,
    asset: CatalogAssetReference | null,
  ) => Promise<void>
}) {
  const { token } = theme.useToken()
  const orderedLocales = [...locales].sort((left, right) =>
    Number(right.id === defaultLocale) - Number(left.id === defaultLocale))
  const textCount = locales.filter((locale) =>
    Boolean(entry.locales[locale.id]?.text?.trim())).length
  const assetCount = locales.filter((locale) =>
    entry.locales[locale.id]?.asset != null).length

  return (
    <InspectorSection
      type={<EntryTypeLabel entryId={entry.id} />}
      icon={renderCatalogNodeIcon('entry', getCatalogNodeIconColor('entry', token))}
      headerContent={<EntryPathInput entry={entry} onPathChange={onPathChange} />}
    >
      <EntryCommentInput entry={entry} onCommentChange={onCommentChange} />
      <Tabs
        defaultActiveKey="text"
        items={[
          {
            key: 'text',
            label: `Text ${textCount}/${locales.length}`,
            children: (
              <Flex vertical gap={layoutTokens.spacing.small}>
                {orderedLocales.map((locale) => (
                  <EntryLocaleCard
                    key={locale.id}
                    locale={locale}
                    defaultLocale={defaultLocale}
                  >
                    <Typography.Text style={{ whiteSpace: 'pre-wrap' }}>
                      {entry.locales[locale.id]?.text ?? '[none]'}
                    </Typography.Text>
                  </EntryLocaleCard>
                ))}
              </Flex>
            ),
          },
          {
            key: 'asset',
            label: `Asset ${assetCount}/${locales.length}`,
            children: (
              <Flex vertical gap={layoutTokens.spacing.small}>
                {orderedLocales.map((locale) => (
                  <EntryLocaleCard
                    key={locale.id}
                    locale={locale}
                    defaultLocale={defaultLocale}
                  >
                    <EntryAssetInput
                      asset={entry.locales[locale.id]?.asset ?? null}
                      onChange={(asset) => onAssetChange(entry.id, locale.id, asset)}
                    />
                  </EntryLocaleCard>
                ))}
              </Flex>
            ),
          },
        ]}
      />
    </InspectorSection>
  )
}

function EntryLocaleCard({
  locale,
  defaultLocale,
  children,
}: {
  locale: CatalogLocale
  defaultLocale: string
  children: ReactNode
}) {
  return (
    <Card
      size="small"
      title={(
        <Flex align="center" gap={layoutTokens.spacing.xSmall}>
          <LocaleFlag culture={locale.culture} />
          <span>{locale.displayName} ({locale.id})</span>
        </Flex>
      )}
      extra={locale.id === defaultLocale ? <Tag color="blue">Default</Tag> : undefined}
    >
      {children}
    </Card>
  )
}

function EntryCommentInput({
  entry,
  onCommentChange,
}: {
  entry: CatalogEntry
  onCommentChange: (id: string, comment: string | null) => Promise<void>
}) {
  const { token } = theme.useToken()
  const [draft, setDraft] = useState(entry.comment ?? '')
  const [error, setError] = useState<string>()
  const [isSaving, setIsSaving] = useState(false)

  useEffect(() => {
    setDraft(entry.comment ?? '')
    setError(undefined)
  }, [entry.comment])

  const commit = async () => {
    const comment = draft.trim() || null
    if (comment === entry.comment) {
      setDraft(entry.comment ?? '')
      setError(undefined)
      return
    }

    setIsSaving(true)
    setError(undefined)
    try {
      await onCommentChange(entry.id, comment)
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : 'Entry comment could not be changed.')
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <Flex vertical gap={layoutTokens.spacing.xSmall}>
      <div style={{ position: 'relative' }}>
        <span
          aria-hidden
          style={{
            position: 'absolute',
            zIndex: 1,
            top: token.paddingXXS,
            left: token.paddingXS,
            color: token.colorTextTertiary,
            fontFamily: token.fontFamilyCode,
            lineHeight: token.lineHeight,
            pointerEvents: 'none',
          }}
        >
          {'//'}
        </span>
        <Input.TextArea
          aria-label="Entry comment"
          autoSize={{ minRows: 1, maxRows: 8 }}
          variant="underlined"
          placeholder="Add a comment…"
          value={draft}
          disabled={isSaving}
          status={error ? 'error' : undefined}
          style={{
            resize: 'none',
            paddingInlineStart: token.paddingXL,
            background: 'transparent',
          }}
          onChange={(event) => {
            setDraft(event.target.value)
            setError(undefined)
          }}
          onBlur={() => void commit()}
          onKeyDown={(event) => {
            if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
              event.preventDefault()
              event.currentTarget.blur()
            } else if (event.key === 'Escape') {
              event.preventDefault()
              event.currentTarget.blur()
            }
          }}
        />
      </div>
      {error && <Typography.Text type="danger">{error}</Typography.Text>}
    </Flex>
  )
}

function EntryTypeLabel({ entryId }: { entryId: string }) {
  const { token } = theme.useToken()

  return (
    <Flex align="center" gap={layoutTokens.spacing.xSmall}>
      <Typography.Text type="secondary">Entry</Typography.Text>
      <span style={{ color: token.colorTextTertiary }}>•</span>
      <Tooltip title="Copy ID">
        <Button
          type="text"
          size="small"
          aria-label={`Copy entry ID ${entryId}`}
          style={{ color: token.colorTextSecondary, paddingInline: layoutTokens.spacing.xSmall }}
          onClick={() => void navigator.clipboard.writeText(entryId).catch(() => undefined)}
        >
          <Flex align="center" gap={layoutTokens.spacing.xSmall}>
            <span>ID: {entryId}</span>
            <CopyOutlined />
          </Flex>
        </Button>
      </Tooltip>
    </Flex>
  )
}

function EntryPathInput({
  entry,
  onPathChange,
}: {
  entry: CatalogEntry
  onPathChange: (id: string, path: string) => Promise<void>
}) {
  const { token } = theme.useToken()
  const [draft, setDraft] = useState(entry.path)
  const [error, setError] = useState<string>()
  const [isSaving, setIsSaving] = useState(false)
  const skipNextBlurRef = useRef(false)
  const inputContainerRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<InputRef>(null)
  const [highlightInsets, setHighlightInsets] = useState({ left: 0, right: 0 })

  useEffect(() => {
    setDraft(entry.path)
    setError(undefined)
  }, [entry.path])

  useLayoutEffect(() => {
    const container = inputContainerRef.current
    const input = inputRef.current?.input
    if (!container || !input) {
      return
    }

    const updateInsets = () => {
      const containerBounds = container.getBoundingClientRect()
      const inputBounds = input.getBoundingClientRect()
      const inputStyle = window.getComputedStyle(input)
      setHighlightInsets({
        left: inputBounds.left - containerBounds.left + Number.parseFloat(inputStyle.paddingLeft),
        right: containerBounds.right - inputBounds.right + Number.parseFloat(inputStyle.paddingRight),
      })
    }

    updateInsets()
    const resizeObserver = new ResizeObserver(updateInsets)
    resizeObserver.observe(container)
    return () => resizeObserver.disconnect()
  }, [])

  const commit = async () => {
    const path = draft.trim()
    if (path === entry.path) {
      setDraft(entry.path)
      setError(undefined)
      return
    }

    if (!isValidEntryPath(path)) {
      setError('Path must contain dot-separated C# identifier segments.')
      return
    }

    setIsSaving(true)
    setError(undefined)
    try {
      await onPathChange(entry.id, path)
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : 'Entry path could not be changed.')
    } finally {
      setIsSaving(false)
    }
  }

  const separatorIndex = draft.lastIndexOf('.')
  const prefix = separatorIndex < 0 ? '' : draft.slice(0, separatorIndex + 1)
  const name = separatorIndex < 0 ? draft : draft.slice(separatorIndex + 1)

  return (
    <Flex vertical gap={layoutTokens.spacing.xSmall}>
      <Flex align="center" gap={layoutTokens.spacing.xSmall}>
        <div ref={inputContainerRef} style={{ position: 'relative', flex: 1, minWidth: 0 }}>
          <div
            aria-hidden
            style={{
              position: 'absolute',
              zIndex: 1,
              top: 0,
              bottom: 0,
              left: highlightInsets.left,
              right: highlightInsets.right,
              display: 'flex',
              alignItems: 'center',
              overflow: 'hidden',
              whiteSpace: 'pre',
              pointerEvents: 'none',
            }}
          >
            <span style={{ color: token.colorTextSecondary }}>{prefix}</span>
            <span style={{ color: token.colorText }}>{name}</span>
          </div>
          <Input
            ref={inputRef}
            addonBefore={<span style={{ color: token.colorTextSecondary }}>Path</span>}
            aria-label="Entry path"
            spellCheck={false}
            value={draft}
            disabled={isSaving}
            status={error ? 'error' : undefined}
            style={{
              position: 'relative',
              zIndex: 0,
            }}
            styles={{
              input: {
                color: 'transparent',
                caretColor: token.colorText,
                WebkitTextFillColor: 'transparent',
              },
            }}
            onChange={(event) => {
              setDraft(event.target.value)
              setError(undefined)
            }}
            onBlur={() => {
              if (skipNextBlurRef.current) {
                skipNextBlurRef.current = false
                return
              }

              void commit()
            }}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                event.preventDefault()
                event.currentTarget.blur()
              } else if (event.key === 'Escape') {
                event.preventDefault()
                skipNextBlurRef.current = true
                setDraft(entry.path)
                setError(undefined)
                event.currentTarget.blur()
              }
            }}
          />
        </div>
        <Tooltip title="Copy path">
          <Button
            size="small"
            type="text"
            icon={<CopyOutlined />}
            aria-label="Copy entry path"
            onClick={() => void navigator.clipboard.writeText(draft)}
          />
        </Tooltip>
      </Flex>
      {error && <Typography.Text type="danger">{error}</Typography.Text>}
    </Flex>
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

function InspectorSection({
  title,
  type,
  icon,
  headerContent,
  children,
}: {
  title?: string
  type: ReactNode
  icon?: ReactNode
  headerContent?: ReactNode
  children: ReactNode
}) {
  const { token } = theme.useToken()

  return (
    <Flex vertical gap={layoutTokens.spacing.large} style={{ width: '100%' }}>
      <div>
        <Flex
          align="center"
          justify="space-between"
          style={{ height: token.controlHeight }}
        >
          <Flex
            align="center"
            gap={layoutTokens.spacing.xSmall}
            style={{ height: '100%', lineHeight: 1 }}
          >
            <span style={{ display: 'flex', alignItems: 'center' }}>{icon}</span>
            {typeof type === 'string'
              ? <Typography.Text type="secondary" style={{ lineHeight: 1 }}>{type}</Typography.Text>
              : type}
          </Flex>
        </Flex>
        {headerContent
          ? <div style={{ marginTop: layoutTokens.spacing.small }}>{headerContent}</div>
          : <Typography.Title level={4} style={{ margin: 0 }}>{title}</Typography.Title>}
      </div>
      {children}
    </Flex>
  )
}

function isValidEntryPath(path: string) {
  return /^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$/.test(path)
}

function AssetReference({ asset }: { asset: CatalogAssetReference | null }) {
  if (!asset) {
    return '[none]'
  }

  return asset.localFileId
    ? `${asset.assetGuid} : ${asset.localFileId}`
    : asset.assetGuid
}
