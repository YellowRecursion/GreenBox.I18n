import {
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type ComponentRef,
  type KeyboardEvent,
  type ReactNode,
} from 'react'
import {
  AppstoreOutlined,
  CloseOutlined,
  CopyOutlined,
  ExpandOutlined,
  WarningOutlined,
} from '@ant-design/icons'
import {
  Alert,
  Button,
  AutoComplete,
  Breadcrumb,
  Descriptions,
  Empty,
  Flex,
  Input,
  Modal,
  Select,
  Space,
  Tag,
  Table,
  Tabs,
  Tooltip,
  Typography,
  theme,
  type InputRef,
  type MenuProps,
} from 'antd'
import { layoutTokens } from '../../design/layoutTokens'
import type { CatalogAssetReference, CatalogEntry, CatalogLocale } from '../../entities/catalog/model/catalog'
import { LocaleFlag } from '../../entities/catalog/ui/LocaleFlag'
import type { CatalogSelectionItem } from './catalogTree'
import { getCatalogNodeIconColor, renderCatalogNodeIcon } from './catalogNodeVisuals'
import { EntryAssetInput } from './EntryAssetInput'
import { commonCultureNames } from './localeCultures'
import { EntryUsageLocations } from './EntryUsageLocations'

interface CatalogInspectorProps {
  selection: CatalogSelectionItem[]
  defaultLocale: string
  locales: CatalogLocale[]
  entries: CatalogEntry[]
  usageCounts?: ReadonlyMap<string, number>
  usageRevision?: string
  onFolderPathChange: (path: string, nextPath: string) => Promise<void>
  onLocaleChange: (locale: CatalogLocale, previousId?: string) => Promise<void>
  onDefaultLocaleChange: (localeId: string) => Promise<void>
  onEntryPathChange: (id: string, path: string) => Promise<void>
  onEntryCommentChange: (id: string, comment: string | null) => Promise<void>
  onEntryTextChange: (id: string, localeId: string, text: string | null) => Promise<void>
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
  entries,
  usageCounts,
  usageRevision,
  onFolderPathChange,
  onLocaleChange,
  onDefaultLocaleChange,
  onEntryPathChange,
  onEntryCommentChange,
  onEntryTextChange,
  onEntryAssetChange,
}: CatalogInspectorProps) {
  if (selection.length === 0) {
    return <Empty description="Select a locale, folder, or entry" />
  }

  if (selection.length > 1) {
    return <MultipleSelectionInspector selection={selection} entries={entries} />
  }

  const item = selection[0]
  switch (item.kind) {
    case 'locale':
      return (
        <LocaleInspector
          locale={item.locale}
          locales={locales}
          entries={entries}
          defaultLocale={defaultLocale}
          onChange={onLocaleChange}
          onDefaultLocaleChange={onDefaultLocaleChange}
        />
      )
    case 'folder':
      return (
        <FolderInspector
          path={item.path}
          entryCount={item.entryCount}
          folderCount={item.folderCount}
          entries={entries}
          locales={locales}
          defaultLocale={defaultLocale}
          onPathChange={onFolderPathChange}
        />
      )
    case 'entry':
      return (
        <EntryInspector
          entry={item.entry}
          defaultLocale={defaultLocale}
          locales={locales}
          usageCount={usageCounts === undefined ? undefined : usageCounts.get(item.entry.id) ?? 0}
          usageRevision={usageRevision}
          onPathChange={onEntryPathChange}
          onCommentChange={onEntryCommentChange}
          onTextChange={onEntryTextChange}
          onAssetChange={onEntryAssetChange}
        />
      )
  }
}

function LocaleInspector({
  locale,
  locales,
  entries,
  defaultLocale,
  onChange,
  onDefaultLocaleChange,
}: {
  locale: CatalogLocale
  locales: CatalogLocale[]
  entries: CatalogEntry[]
  defaultLocale: string
  onChange: (locale: CatalogLocale, previousId?: string) => Promise<void>
  onDefaultLocaleChange: (localeId: string) => Promise<void>
}) {
  const [error, setError] = useState<string>()
  const [isChanging, setIsChanging] = useState(false)
  const isDefault = locale.id === defaultLocale
  const textCount = entries.filter((entry) => Boolean(entry.locales[locale.id]?.text?.trim())).length
  const assetCount = entries.filter((entry) => entry.locales[locale.id]?.asset != null).length
  const fallbackUsers = locales.filter((candidate) => candidate.fallback === locale.id)
  const cultureOptions = [...new Set([
    ...locales.map((candidate) => candidate.culture),
    ...commonCultureNames,
  ])].map((value) => ({ value }))

  const apply = async (
    change: CatalogLocale,
    fallbackError: string,
    previousId?: string,
  ) => {
    setIsChanging(true)
    setError(undefined)
    try {
      await onChange(change, previousId)
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : fallbackError)
      throw reason
    } finally {
      setIsChanging(false)
    }
  }

  const setDefault = async () => {
    setIsChanging(true)
    setError(undefined)
    try {
      await onDefaultLocaleChange(locale.id)
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : 'Default locale could not be changed.')
    } finally {
      setIsChanging(false)
    }
  }

  return (
    <InspectorSection
      type="Locale"
      icon={<LocaleFlag culture={locale.culture} />}
    >
      <Flex vertical gap={layoutTokens.spacing.large}>
        <LocaleIdField
          key={`${locale.id}:id`}
          locale={locale}
          disabled={isChanging}
          onChange={(id) => apply(
            { ...locale, id },
            'Locale ID could not be changed.',
            locale.id,
          )}
        />
        <LocaleTextField
          key={`${locale.id}:displayName`}
          label="Display name"
          value={locale.displayName}
          disabled={isChanging}
          onChange={(displayName) => apply(
            { ...locale, displayName },
            'Locale display name could not be changed.',
          )}
        />
        <LocaleTextField
          key={`${locale.id}:culture`}
          label="Culture"
          value={locale.culture}
          suggestions={cultureOptions}
          disabled={isChanging}
          onChange={(culture) => apply(
            { ...locale, culture },
            'Locale culture could not be changed.',
          )}
        />
        <Flex vertical gap={layoutTokens.spacing.xSmall}>
          <Space.Compact block>
            <LocaleFieldAddon>Fallback</LocaleFieldAddon>
            <Select
              aria-label="Locale fallback"
              value={locale.fallback ?? ''}
              disabled={isDefault || isChanging}
              style={{ flex: 1 }}
              options={[
                { value: '', label: 'None' },
                ...locales
                  .filter((candidate) => candidate.id !== locale.id)
                  .map((candidate) => ({
                    value: candidate.id,
                    label: (
                      <Flex align="center" gap={layoutTokens.spacing.xSmall}>
                        <LocaleFlag culture={candidate.culture} />
                        <span>{candidate.displayName}</span>
                      </Flex>
                    ),
                  })),
              ]}
              onChange={(fallback) => {
                void apply(
                  { ...locale, fallback: fallback || null },
                  'Locale fallback could not be changed.',
                ).catch(() => undefined)
              }}
            />
          </Space.Compact>
          {isDefault && (
            <Typography.Text type="secondary">The default locale cannot have a fallback.</Typography.Text>
          )}
        </Flex>
        <Flex align="center" justify="space-between" gap={layoutTokens.spacing.large}>
          <div>
            <Typography.Text strong>Default locale</Typography.Text>
            <br />
            <Typography.Text type="secondary">
              Used when no locale is selected.
            </Typography.Text>
          </div>
          {isDefault
            ? <Tag color="blue" style={{ marginInlineEnd: 0 }}>Default</Tag>
            : <Button disabled={isChanging} onClick={() => void setDefault()}>Set as default</Button>}
        </Flex>
        <Flex vertical gap={layoutTokens.spacing.xSmall}>
          <Typography.Text strong>Icon</Typography.Text>
          <EntryAssetInput
            asset={locale.icon}
            onChange={(icon) => apply(
              { ...locale, icon },
              'Locale icon could not be changed.',
            )}
          />
        </Flex>
        <Flex vertical gap={layoutTokens.spacing.small}>
          <Typography.Text strong>Usage</Typography.Text>
          <Descriptions
            size="small"
            column={1}
            items={[
              { key: 'text', label: 'Text translations', children: `${textCount}/${entries.length}` },
              { key: 'asset', label: 'Asset assignments', children: `${assetCount}/${entries.length}` },
              {
                key: 'fallbackUsers',
                label: 'Used as fallback by',
                children: fallbackUsers.length === 0
                  ? '0 locales'
                  : fallbackUsers.map((candidate) => candidate.displayName).join(', '),
              },
            ]}
          />
        </Flex>
        {error && <Typography.Text type="danger">{error}</Typography.Text>}
      </Flex>
    </InspectorSection>
  )
}

function LocaleIdField({
  locale,
  disabled,
  onChange,
}: {
  locale: CatalogLocale
  disabled: boolean
  onChange: (id: string) => Promise<void>
}) {
  const { token } = theme.useToken()
  const [draft, setDraft] = useState(locale.id)
  const [error, setError] = useState<string>()
  const [isSaving, setIsSaving] = useState(false)

  const commit = async () => {
    const id = draft.trim()
    if (id === locale.id) {
      setDraft(locale.id)
      setError(undefined)
      return
    }

    if (!/^[A-Za-z][A-Za-z0-9]*(?:-[A-Za-z0-9]+)*$/.test(id)) {
      setError('Use hyphen-separated ASCII letter and digit segments.')
      return
    }

    setIsSaving(true)
    setError(undefined)
    try {
      await onChange(id)
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : 'Locale ID could not be changed.')
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <Flex vertical gap={layoutTokens.spacing.xSmall}>
      <Space.Compact block>
        <LocaleFieldAddon>ID</LocaleFieldAddon>
        <Input
          aria-label="Locale ID"
          value={draft}
          disabled={disabled || isSaving}
          status={error ? 'error' : undefined}
          onChange={(event) => {
            setDraft(event.target.value)
            setError(undefined)
          }}
          onBlur={() => void commit()}
          onKeyDown={(event) => handleSingleLineCommitKey(event)}
        />
        <Tooltip title="Copy ID">
          <Button
            aria-label={`Copy locale ID ${locale.id}`}
            icon={<CopyOutlined />}
            onMouseDown={(event) => event.preventDefault()}
            onClick={() => void navigator.clipboard.writeText(locale.id).catch(() => undefined)}
          />
        </Tooltip>
      </Space.Compact>
      {error
        ? <Typography.Text type="danger">{error}</Typography.Text>
        : (
            <Typography.Text style={{ color: token.colorWarning }}>
              <WarningOutlined /> Renaming updates this catalog, but not external references.
            </Typography.Text>
          )}
    </Flex>
  )
}

function LocaleTextField({
  label,
  value,
  suggestions,
  disabled,
  onChange,
}: {
  label: string
  value: string
  suggestions?: { value: string }[]
  disabled: boolean
  onChange: (value: string) => Promise<void>
}) {
  const [draft, setDraft] = useState(value)
  const [error, setError] = useState<string>()
  const [isSaving, setIsSaving] = useState(false)

  useEffect(() => {
    setDraft(value)
    setError(undefined)
  }, [value])

  const commit = async () => {
    const nextValue = draft.trim()
    if (nextValue === value) {
      setDraft(value)
      return
    }

    setIsSaving(true)
    setError(undefined)
    try {
      await onChange(nextValue)
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : `${label} could not be changed.`)
    } finally {
      setIsSaving(false)
    }
  }

  const input = suggestions
    ? (
        <AutoComplete
          aria-label={label}
          value={draft}
          options={suggestions}
          disabled={disabled || isSaving}
          status={error ? 'error' : undefined}
          style={{ flex: 1 }}
          filterOption={(inputValue, option) =>
            (option?.value ?? '').toLowerCase().includes(inputValue.toLowerCase())}
          onChange={(nextValue) => {
            setDraft(nextValue)
            setError(undefined)
          }}
          onBlur={() => void commit()}
          onKeyDown={(event) => handleSingleLineCommitKey(event)}
        />
      )
    : (
        <Input
          aria-label={label}
          value={draft}
          disabled={disabled || isSaving}
          status={error ? 'error' : undefined}
          style={{ flex: 1 }}
          onChange={(event) => {
            setDraft(event.target.value)
            setError(undefined)
          }}
          onBlur={() => void commit()}
          onKeyDown={(event) => handleSingleLineCommitKey(event)}
        />
      )

  return (
    <Flex vertical gap={layoutTokens.spacing.xSmall}>
      <Space.Compact block>
        <LocaleFieldAddon>{label}</LocaleFieldAddon>
        {input}
      </Space.Compact>
      {error && <Typography.Text type="danger">{error}</Typography.Text>}
    </Flex>
  )
}

function LocaleFieldAddon({ children }: { children: ReactNode }) {
  return (
    <Space.Addon style={{ flex: '0 0 112px', justifyContent: 'flex-start' }}>
      {children}
    </Space.Addon>
  )
}

function handleSingleLineCommitKey(event: KeyboardEvent<HTMLElement>) {
  if (event.key === 'Enter' || event.key === 'Escape') {
    event.preventDefault()
    event.currentTarget.blur()
  }
}

function FolderInspector({
  path,
  entryCount,
  folderCount,
  entries,
  locales,
  defaultLocale,
  onPathChange,
}: {
  path: string
  entryCount: number
  folderCount: number
  entries: CatalogEntry[]
  locales: CatalogLocale[]
  defaultLocale: string
  onPathChange: (path: string, nextPath: string) => Promise<void>
}) {
  const { token } = theme.useToken()
  const folderEntries = entries.filter((entry) => entry.path.startsWith(`${path}.`))
  const orderedLocales = [...locales].sort((left, right) =>
    Number(right.id === defaultLocale) - Number(left.id === defaultLocale))
  const coverage = orderedLocales.map((locale) => ({
    key: locale.id,
    locale,
    textCount: folderEntries.filter((entry) =>
      Boolean(entry.locales[locale.id]?.text?.trim())).length,
    assetCount: folderEntries.filter((entry) =>
      entry.locales[locale.id]?.asset != null).length,
  }))

  return (
    <InspectorSection
      type="Folder"
      icon={renderCatalogNodeIcon('folder', getCatalogNodeIconColor('folder', token))}
      headerContent={(
        <CatalogPathInput
          path={path}
          ariaLabel="Folder path"
          onChange={(nextPath) => onPathChange(path, nextPath)}
        />
      )}
    >
      <Flex vertical gap={layoutTokens.spacing.large}>
        <Flex vertical gap={layoutTokens.spacing.xSmall}>
          <Typography.Text strong>Contents</Typography.Text>
          <Typography.Text type="secondary">
            {entryCount} {entryCount === 1 ? 'entry' : 'entries'} ·{' '}
            {folderCount} {folderCount === 1 ? 'subfolder' : 'subfolders'}
          </Typography.Text>
        </Flex>
        <Flex vertical gap={layoutTokens.spacing.small}>
          <Typography.Text strong>Localization</Typography.Text>
          <Table
            size="small"
            pagination={false}
            dataSource={coverage}
            columns={[
              {
                title: 'Locale',
                dataIndex: 'locale',
                render: (locale: CatalogLocale) => (
                  <Flex align="center" gap={layoutTokens.spacing.xSmall}>
                    <LocaleFlag culture={locale.culture} />
                    <span>{locale.displayName}</span>
                    {locale.id === defaultLocale && <Tag color="blue">Default</Tag>}
                  </Flex>
                ),
              },
              {
                title: 'Text',
                dataIndex: 'textCount',
                width: 88,
                render: (count: number) => `${count}/${entryCount}`,
              },
              {
                title: 'Asset',
                dataIndex: 'assetCount',
                width: 88,
                render: (count: number) => `${count}/${entryCount}`,
              },
            ]}
          />
        </Flex>
      </Flex>
    </InspectorSection>
  )
}

function EntryInspector({
  entry,
  defaultLocale,
  locales,
  usageCount,
  usageRevision,
  onPathChange,
  onCommentChange,
  onTextChange,
  onAssetChange,
}: {
  entry: CatalogEntry
  defaultLocale: string
  locales: CatalogLocale[]
  usageCount?: number
  usageRevision?: string
  onPathChange: (id: string, path: string) => Promise<void>
  onCommentChange: (id: string, comment: string | null) => Promise<void>
  onTextChange: (id: string, localeId: string, text: string | null) => Promise<void>
  onAssetChange: (
    id: string,
    localeId: string,
    asset: CatalogAssetReference | null,
  ) => Promise<void>
}) {
  const { token } = theme.useToken()
  const [expandedLocaleId, setExpandedLocaleId] = useState<string>()
  const [activeTab, setActiveTab] = useState('text')
  const orderedLocales = [...locales].sort((left, right) =>
    Number(right.id === defaultLocale) - Number(left.id === defaultLocale))
  const textCount = locales.filter((locale) =>
    Boolean(entry.locales[locale.id]?.text?.trim())).length
  const assetCount = locales.filter((locale) =>
    entry.locales[locale.id]?.asset != null).length
  useEffect(() => {
    if (activeTab === 'usage' && usageCount === undefined) {
      setActiveTab('text')
    }
  }, [activeTab, usageCount])

  return (
    <InspectorSection
      type={<EntryTypeLabel entryId={entry.id} />}
      icon={renderCatalogNodeIcon('entry', getCatalogNodeIconColor('entry', token))}
      headerContent={(
        <CatalogPathInput
          path={entry.path}
          ariaLabel="Entry path"
          onChange={(path) => onPathChange(entry.id, path)}
        />
      )}
    >
      <EntryCommentInput entry={entry} onCommentChange={onCommentChange} />
      <Tabs
        activeKey={activeTab}
        onChange={setActiveTab}
        items={[
          {
            key: 'text',
            label: `Text ${textCount}/${locales.length}`,
            children: (
              <Flex
                vertical
                gap={layoutTokens.spacing.large}
                style={{ marginTop: layoutTokens.spacing.large }}
              >
                {orderedLocales.map((locale) => (
                  <EntryTextInput
                    key={`${entry.id}:${locale.id}`}
                    entryPath={entry.path}
                    value={entry.locales[locale.id]?.text ?? null}
                    locale={locale}
                    locales={orderedLocales}
                    defaultLocale={defaultLocale}
                    isExpanded={expandedLocaleId === locale.id}
                    onExpandedLocaleChange={setExpandedLocaleId}
                    onChange={(text) => onTextChange(entry.id, locale.id, text)}
                  />
                ))}
              </Flex>
            ),
          },
          {
            key: 'asset',
            label: `Asset ${assetCount}/${locales.length}`,
            children: (
              <Flex
                vertical
                gap={layoutTokens.spacing.large}
                style={{ marginTop: layoutTokens.spacing.large }}
              >
                {orderedLocales.map((locale) => (
                  <Flex key={locale.id} vertical gap={layoutTokens.spacing.xSmall}>
                    <Flex align="center" gap={layoutTokens.spacing.xSmall}>
                      <LocaleFlag culture={locale.culture} />
                      <Typography.Text>{locale.displayName}</Typography.Text>
                      {locale.id === defaultLocale && <Tag color="blue">Default</Tag>}
                    </Flex>
                    <EntryAssetInput
                      asset={entry.locales[locale.id]?.asset ?? null}
                      onChange={(asset) => onAssetChange(entry.id, locale.id, asset)}
                    />
                  </Flex>
                ))}
              </Flex>
            ),
          },
          ...(usageCount === undefined
            ? []
            : [{
                key: 'usage',
                label: (
                  <Flex align="center" gap={layoutTokens.spacing.xSmall}>
                    {usageCount === 0
                      ? <WarningOutlined style={{ color: token.colorWarning }} />
                      : null}
                    <span>Usage</span>
                    {usageCount > 0 && (
                      <Typography.Text style={{ color: token.colorInfo }}>{usageCount}</Typography.Text>
                    )}
                  </Flex>
                ),
                children: usageCount === 0
                  ? (
                      <Alert
                        showIcon
                        type="warning"
                        message="No usages found"
                        description="This entry is not referenced by C# code or serialized Unity assets."
                      />
                    )
                  : (
                      <EntryUsageLocations
                        entryId={entry.id}
                        usageRevision={usageRevision}
                        enabled={activeTab === 'usage'}
                      />
                    ),
              }]),
        ]}
      />
    </InspectorSection>
  )
}

function EntryTextInput({
  entryPath,
  value,
  locale,
  locales,
  defaultLocale,
  isExpanded,
  onExpandedLocaleChange,
  onChange,
}: {
  entryPath: string
  value: string | null
  locale: CatalogLocale
  locales: CatalogLocale[]
  defaultLocale: string
  isExpanded: boolean
  onExpandedLocaleChange: (localeId: string | undefined) => void
  onChange: (text: string | null) => Promise<void>
}) {
  const [draft, setDraft] = useState(value ?? '')
  const [error, setError] = useState<string>()
  const [isSaving, setIsSaving] = useState(false)
  const skipCompactBlurRef = useRef(false)
  const focusEditorRef = useRef<ComponentRef<typeof Input.TextArea>>(null)

  useEffect(() => {
    setDraft(value ?? '')
    setError(undefined)
  }, [value])

  const commit = async () => {
    const persistedText = value ?? ''
    if (draft === persistedText) {
      setError(undefined)
      return true
    }

    setIsSaving(true)
    setError(undefined)
    try {
      await onChange(draft === '' ? null : draft)
      return true
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : 'Localized text could not be changed.')
      return false
    } finally {
      setIsSaving(false)
    }
  }

  const openExpanded = () => {
    skipCompactBlurRef.current = true
    onExpandedLocaleChange(locale.id)
  }

  const closeExpanded = async (nextLocaleId?: string) => {
    if (isSaving) {
      return
    }

    if (await commit()) {
      onExpandedLocaleChange(nextLocaleId)
    }
  }

  const handleLocaleMenuClick: MenuProps['onClick'] = ({ key }) => {
    void closeExpanded(String(key))
  }

  return (
    <Flex vertical gap={layoutTokens.spacing.xSmall}>
      <Flex align="center" justify="space-between" gap={layoutTokens.spacing.small}>
        <Flex align="center" gap={layoutTokens.spacing.xSmall}>
          <LocaleFlag culture={locale.culture} />
          <Typography.Text>{locale.displayName}</Typography.Text>
          {locale.id === defaultLocale && <Tag color="blue">Default</Tag>}
        </Flex>
        <Flex align="center" gap={layoutTokens.spacing.small}>
          <TrailingWhitespaceIndicator text={draft} />
          <Tooltip title="Copy translation">
            <Button
              type="text"
              size="small"
              aria-label={`Copy ${locale.displayName} translation`}
              icon={<CopyOutlined />}
              onMouseDown={(event) => event.preventDefault()}
              onClick={() => void navigator.clipboard.writeText(draft).catch(() => undefined)}
            />
          </Tooltip>
          <Tooltip title="Open focus editor">
            <Button
              type="text"
              size="small"
              aria-label={`Open ${locale.displayName} focus editor`}
              icon={<ExpandOutlined />}
              onMouseDown={(event) => event.preventDefault()}
              onClick={openExpanded}
            />
          </Tooltip>
        </Flex>
      </Flex>
      <Input.TextArea
        aria-label={`${locale.displayName} localized text`}
        autoSize={{ minRows: 1, maxRows: 3 }}
        placeholder="No translation"
        value={draft}
        disabled={isSaving}
        status={error ? 'error' : undefined}
        style={{ resize: 'none', background: 'transparent' }}
        onChange={(event) => {
          setDraft(event.target.value)
          setError(undefined)
        }}
        onBlur={() => {
          if (skipCompactBlurRef.current) {
            skipCompactBlurRef.current = false
          } else {
            void commit()
          }
        }}
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
      {error && <Typography.Text type="danger">{error}</Typography.Text>}
      <Modal
        open={isExpanded}
        centered
        width={800}
        footer={null}
        keyboard
        maskClosable={false}
        closable={false}
        title={(
          <Flex align="center" justify="space-between" gap={layoutTokens.spacing.large}>
            <div style={{ minWidth: 0, overflow: 'hidden', whiteSpace: 'nowrap' }}>
              <Breadcrumb
                items={[
                  {
                    title: entryPath,
                    style: { cursor: 'pointer' },
                    onClick: () => void closeExpanded(),
                  },
                  {
                    title: (
                      <Space size={layoutTokens.spacing.xSmall}>
                        <LocaleFlag culture={locale.culture} />
                        <span>{locale.displayName}</span>
                      </Space>
                    ),
                    menu: {
                      items: locales.map((candidate) => ({
                        key: candidate.id,
                        disabled: candidate.id === locale.id,
                        label: (
                          <Flex align="center" gap={layoutTokens.spacing.xSmall}>
                            <LocaleFlag culture={candidate.culture} />
                            <span>{candidate.displayName}</span>
                            {candidate.id === defaultLocale && <Tag color="blue">Default</Tag>}
                          </Flex>
                        ),
                      })),
                      onClick: handleLocaleMenuClick,
                    },
                  },
                ]}
              />
            </div>
            <Button
              type="text"
              aria-label="Close focus editor"
              icon={<CloseOutlined />}
              disabled={isSaving}
              onClick={() => void closeExpanded()}
            />
          </Flex>
        )}
        styles={{
          container: {
            height: '90vh',
            display: 'flex',
            flexDirection: 'column',
            overflow: 'hidden',
          },
          body: {
            flex: 1,
            minHeight: 0,
            display: 'flex',
            flexDirection: 'column',
            overflow: 'hidden',
          },
        }}
        afterOpenChange={(open) => {
          if (open) {
            skipCompactBlurRef.current = false
            focusEditorRef.current?.focus({ cursor: 'end' })
          }
        }}
        onCancel={() => void closeExpanded()}
      >
        <Input.TextArea
          ref={focusEditorRef}
          aria-label={`${locale.displayName} focus editor`}
          placeholder="No translation"
          value={draft}
          disabled={isSaving}
          status={error ? 'error' : undefined}
          style={{
            flex: 1,
            minHeight: 0,
            resize: 'none',
            overflow: 'auto',
            background: 'transparent',
          }}
          onChange={(event) => {
            setDraft(event.target.value)
            setError(undefined)
          }}
        />
        {error && (
          <Typography.Text type="danger" style={{ marginTop: layoutTokens.spacing.xSmall }}>
            {error}
          </Typography.Text>
        )}
      </Modal>
    </Flex>
  )
}

function TrailingWhitespaceIndicator({ text }: { text: string }) {
  const { token } = theme.useToken()
  const trailingWhitespace = text.match(/[ \t\r\n]+$/)?.[0] ?? ''
  const spaceCount = trailingWhitespace.split('').filter((character) => character === ' ').length
  const lineBreakCount = trailingWhitespace.split('').filter((character) => character === '\n').length
  if (spaceCount === 0 && lineBreakCount === 0) {
    return null
  }

  const details = [
    spaceCount > 0 ? `${spaceCount} trailing ${spaceCount === 1 ? 'space' : 'spaces'}` : null,
    lineBreakCount > 0
      ? `${lineBreakCount} trailing line ${lineBreakCount === 1 ? 'break' : 'breaks'}`
      : null,
  ].filter((detail): detail is string => detail != null).join('; ')

  return (
    <Tooltip title={details}>
      <Flex align="center" gap={layoutTokens.spacing.small} style={{ flex: '0 0 auto' }}>
        {spaceCount > 0 && (
          <Typography.Text style={{ color: token.colorWarning }}>· ×{spaceCount}</Typography.Text>
        )}
        {lineBreakCount > 0 && (
          <Typography.Text style={{ color: token.colorWarning }}>↵ ×{lineBreakCount}</Typography.Text>
        )}
      </Flex>
    </Tooltip>
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
      <Tooltip title="Copy ID">
        <Button
          type="text"
          size="small"
          aria-label={`Copy entry ID ${entryId}`}
          style={{ color: token.colorTextSecondary, paddingInline: layoutTokens.spacing.xSmall }}
          onClick={() => void navigator.clipboard.writeText(entryId).catch(() => undefined)}
        >
          <Flex align="center" gap={layoutTokens.spacing.xSmall}>
            <span>{entryId}</span>
            <CopyOutlined />
          </Flex>
        </Button>
      </Tooltip>
    </Flex>
  )
}

function CatalogPathInput({
  path,
  ariaLabel,
  onChange,
}: {
  path: string
  ariaLabel: string
  onChange: (path: string) => Promise<void>
}) {
  const { token } = theme.useToken()
  const [draft, setDraft] = useState(path)
  const [error, setError] = useState<string>()
  const [isSaving, setIsSaving] = useState(false)
  const skipNextBlurRef = useRef(false)
  const inputContainerRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<InputRef>(null)
  const [highlightInsets, setHighlightInsets] = useState({ left: 0, right: 0 })

  useEffect(() => {
    setDraft(path)
    setError(undefined)
  }, [path])

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
    const nextPath = draft.trim()
    if (nextPath === path) {
      setDraft(path)
      setError(undefined)
      return
    }

    if (!isValidEntryPath(nextPath)) {
      setError('Path must contain dot-separated C# identifier segments.')
      return
    }

    setIsSaving(true)
    setError(undefined)
    try {
      await onChange(nextPath)
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : 'Path could not be changed.')
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
        <div
          ref={inputContainerRef}
          style={{ position: 'relative', flex: 1, minWidth: 0 }}
        >
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
          <Space.Compact block>
            <Space.Addon>Path</Space.Addon>
            <Input
              ref={inputRef}
              aria-label={ariaLabel}
              spellCheck={false}
              value={draft}
              disabled={isSaving}
              status={error ? 'error' : undefined}
              style={{ position: 'relative', zIndex: 0 }}
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
                  setDraft(path)
                  setError(undefined)
                  event.currentTarget.blur()
                }
              }}
            />
          </Space.Compact>
        </div>
        <Tooltip title="Copy path">
          <Button
            size="small"
            type="text"
            icon={<CopyOutlined />}
            aria-label={`Copy ${ariaLabel.toLocaleLowerCase()}`}
            onClick={() => void navigator.clipboard.writeText(draft)}
          />
        </Tooltip>
      </Flex>
      {error && <Typography.Text type="danger">{error}</Typography.Text>}
    </Flex>
  )
}

function MultipleSelectionInspector({
  selection,
  entries,
}: {
  selection: CatalogSelectionItem[]
  entries: CatalogEntry[]
}) {
  const { token } = theme.useToken()
  const localeCount = selection.filter((item) => item.kind === 'locale').length
  const folderCount = selection.filter((item) => item.kind === 'folder').length
  const entryCount = selection.filter((item) => item.kind === 'entry').length
  const selectedEntryIds = new Set(selection
    .filter((item) => item.kind === 'entry')
    .map((item) => item.entry.id))
  const selectedFolderPaths = selection
    .filter((item) => item.kind === 'folder')
    .map((item) => item.path)
  const expandedEntries = entries.filter((entry) =>
    selectedEntryIds.has(entry.id) ||
    selectedFolderPaths.some((path) => entry.path.startsWith(`${path}.`)))
  const expandedFolderPaths = new Set(selectedFolderPaths)
  for (const entry of expandedEntries) {
    const segments = entry.path.split('.').slice(0, -1)
    for (let index = 0; index < segments.length; index++) {
      const path = segments.slice(0, index + 1).join('.')
      if (selectedFolderPaths.some((selectedPath) =>
        path === selectedPath || path.startsWith(`${selectedPath}.`))) {
        expandedFolderPaths.add(path)
      }
    }
  }
  const selectedKindCount = [localeCount, folderCount, entryCount].filter((count) => count > 0).length
  const headerIcon = selectedKindCount === 1
    ? localeCount > 0
      ? renderCatalogNodeIcon('locale', getCatalogNodeIconColor('locale', token))
      : folderCount > 0
        ? renderCatalogNodeIcon('folder', getCatalogNodeIconColor('folder', token))
        : renderCatalogNodeIcon('entry', getCatalogNodeIconColor('entry', token))
    : <AppstoreOutlined style={{ color: token.colorTextSecondary }} />

  return (
    <InspectorSection
      type={(
        <Flex align="center" gap={layoutTokens.spacing.xSmall}>
          <Typography.Text type="secondary">Multiple selection</Typography.Text>
          <Typography.Text type="secondary">{selection.length}</Typography.Text>
        </Flex>
      )}
      icon={headerIcon}
      contentGap={layoutTokens.spacing.small}
    >
      <Flex vertical gap={0}>
        {localeCount > 0 && (
          <SelectionSummaryRow
            icon={renderCatalogNodeIcon('locale', getCatalogNodeIconColor('locale', token))}
            label="Locales"
            value={localeCount}
          />
        )}
        {folderCount > 0 && (
          <SelectionSummaryRow
            icon={renderCatalogNodeIcon('folder', getCatalogNodeIconColor('folder', token))}
            label="Folders"
            value={folderCount}
            total={expandedFolderPaths.size}
          />
        )}
        {expandedEntries.length > 0 && (
          <SelectionSummaryRow
            icon={renderCatalogNodeIcon('entry', getCatalogNodeIconColor('entry', token))}
            label="Entries"
            value={entryCount}
            total={expandedEntries.length}
          />
        )}
      </Flex>
    </InspectorSection>
  )
}

function SelectionSummaryRow({
  icon,
  label,
  value,
  total,
}: {
  icon: ReactNode
  label: string
  value: number
  total?: number
}) {
  return (
    <Flex align="center" gap={layoutTokens.spacing.small} style={{ minHeight: 22 }}>
      <span style={{ display: 'flex', alignItems: 'center' }}>{icon}</span>
      <Typography.Text>{label}</Typography.Text>
      <Typography.Text type="secondary">
        {value}{total !== undefined && total !== value ? ` (total ${total})` : ''}
      </Typography.Text>
    </Flex>
  )
}

function InspectorSection({
  title,
  type,
  icon,
  headerContent,
  contentGap = layoutTokens.spacing.large,
  children,
}: {
  title?: string
  type: ReactNode
  icon?: ReactNode
  headerContent?: ReactNode
  contentGap?: number
  children: ReactNode
}) {
  const { token } = theme.useToken()

  return (
    <Flex vertical gap={contentGap} style={{ width: '100%' }}>
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
          : title
            ? <Typography.Title level={4} style={{ margin: 0 }}>{title}</Typography.Title>
            : null}
      </div>
      {children}
    </Flex>
  )
}

function isValidEntryPath(path: string) {
  return /^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$/.test(path)
}
