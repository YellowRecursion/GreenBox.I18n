import { DownOutlined, SaveOutlined, TranslationOutlined, UndoOutlined } from '@ant-design/icons'
import { useCallback, useEffect, useState } from 'react'
import { Button, Dropdown, Flex, Modal, Space, Typography, message, theme, type MenuProps } from 'antd'
import { layoutTokens } from '../../design/layoutTokens'
import { OpenCatalogDialog } from '../../features/open-catalog/OpenCatalogDialog'
import { HttpError } from '../../shared/api/httpClient'
import type { CatalogSourceStatus } from '../../entities/catalog/api/getCatalogSourceStatus'

interface EditorHeaderProps {
  catalogPath: string
  isDirty: boolean
  sourceStatus: CatalogSourceStatus
  onSave(overwriteExternalChanges?: boolean): Promise<void>
  onRevert(): Promise<void>
}

export function EditorHeader({ catalogPath, isDirty, sourceStatus, onSave, onRevert }: EditorHeaderProps) {
  const { token } = theme.useToken()
  const [isSaving, setIsSaving] = useState(false)
  const [isReverting, setIsReverting] = useState(false)
  const [hasExternalConflict, setHasExternalConflict] = useState(false)
  const [messageApi, messageContext] = message.useMessage()
  const [modalApi, modalContext] = Modal.useModal()

  const save = useCallback(async (overwriteExternalChanges = false) => {
    if (sourceStatus.hasChanged && !overwriteExternalChanges) {
      setHasExternalConflict(true)
      return
    }

    setIsSaving(true)
    try {
      await onSave(overwriteExternalChanges)
      setHasExternalConflict(false)
      messageApi.success('Catalog saved.')
    } catch (error: unknown) {
      if (error instanceof HttpError && error.code === 'catalog_changed_externally') {
        setHasExternalConflict(true)
      } else {
        messageApi.error(error instanceof Error ? error.message : 'Catalog could not be saved.')
      }
    } finally {
      setIsSaving(false)
    }
  }, [messageApi, onSave, sourceStatus.hasChanged])

  const revert = useCallback(async () => {
    setIsReverting(true)
    try {
      await onRevert()
      setHasExternalConflict(false)
      messageApi.success('Changes reverted.')
    } catch (error: unknown) {
      messageApi.error(error instanceof Error ? error.message : 'Changes could not be reverted.')
    } finally {
      setIsReverting(false)
    }
  }, [messageApi, onRevert])

  const confirmRevert = useCallback(() => {
    modalApi.confirm({
      title: 'Revert unsaved changes?',
      content: 'The working copy will be replaced with the current catalog file from disk.',
      okText: 'Revert changes',
      okButtonProps: { danger: true },
      onOk: revert,
    })
  }, [modalApi, revert])

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLocaleLowerCase() === 's') {
        event.preventDefault()
        if (isDirty && !isSaving) {
          void save()
        }
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [isDirty, isSaving, save])

  const saveMenu: MenuProps = {
    items: [
      {
        key: 'revert',
        icon: <UndoOutlined />,
        label: 'Revert changes',
        danger: true,
        disabled: !isDirty || isSaving || isReverting,
      },
    ],
    onClick: ({ key }) => {
      if (key === 'revert') {
        confirmRevert()
      }
    },
  }

  return (
    <Flex
      align="center"
      gap={layoutTokens.spacing.large}
      style={{
        height: 52,
        flex: '0 0 52px',
        padding: `0 ${layoutTokens.spacing.large}px`,
        borderBottom: `1px solid ${token.colorBorderSecondary}`,
        background: token.colorBgContainer,
        whiteSpace: 'nowrap',
      }}
    >
      {messageContext}
      {modalContext}
      <Flex align="center" gap={layoutTokens.spacing.small}>
        <TranslationOutlined style={{ color: token.colorPrimary, fontSize: 20 }} />
        <Typography.Text strong>GreenBox.I18n</Typography.Text>
      </Flex>

      <div style={{ width: 1, height: 20, background: token.colorBorderSecondary }} />

      <Flex align="center" gap={layoutTokens.spacing.xSmall} style={{ minWidth: 0 }} title={catalogPath}>
        <div style={{ minWidth: 0, whiteSpace: 'nowrap' }}>
          <Typography.Text type="secondary">{truncateMiddle(catalogPath, 64)}</Typography.Text>
        </div>
        <OpenCatalogDialog catalogPath={catalogPath} />
      </Flex>

      <div style={{ flex: 1 }} />

      {sourceStatus.hasChanged ? (
        <Flex align="center" gap={layoutTokens.spacing.xSmall} title={sourceStatus.message ?? undefined}>
          <span
            style={{
              width: 8,
              height: 8,
              borderRadius: '50%',
              background: sourceStatus.isAvailable ? token.colorWarning : token.colorError,
            }}
          />
          <Typography.Text
            style={{ color: sourceStatus.isAvailable ? token.colorWarning : token.colorError }}
          >
            {sourceStatus.isAvailable ? 'Changed on disk' : 'Source unavailable'}
          </Typography.Text>
        </Flex>
      ) : isDirty && (
        <Flex align="center" gap={layoutTokens.spacing.xSmall}>
          <span
            style={{
              width: 8,
              height: 8,
              borderRadius: '50%',
              background: token.colorWarning,
            }}
          />
          <Typography.Text style={{ color: token.colorWarning }}>
            Unsaved changes
          </Typography.Text>
        </Flex>
      )}

      <Space.Compact>
        <Button
          type="primary"
          disabled={!isDirty}
          loading={isSaving}
          onClick={() => void save()}
        >
          <SaveOutlined />
          <span>Save</span>
          <span style={{ opacity: 0.72, fontSize: 12 }}>Ctrl+S</span>
        </Button>
        <Dropdown menu={saveMenu} trigger={['click']} disabled={!isDirty || isSaving}>
          <Button
            type="primary"
            icon={<DownOutlined />}
            aria-label="More save actions"
          />
        </Dropdown>
      </Space.Compact>

      <Modal
        open={hasExternalConflict}
        title="Catalog changed on disk"
        onCancel={() => setHasExternalConflict(false)}
        footer={[
          <Button key="cancel" onClick={() => setHasExternalConflict(false)}>
            Cancel
          </Button>,
          <Button
            key="overwrite"
            danger
            loading={isSaving}
            onClick={() => void save(true)}
          >
            Overwrite
          </Button>,
          <Button
            key="reload"
            type="primary"
            loading={isReverting}
            onClick={() => void revert()}
          >
            Reload from disk
          </Button>,
        ]}
      >
        The source JSON was modified after this editor loaded it. Reload the external version or explicitly overwrite it.
      </Modal>
    </Flex>
  )
}

function truncateMiddle(value: string, maxLength: number) {
  if (value.length <= maxLength) {
    return value
  }

  const availableLength = maxLength - 1
  const startLength = Math.ceil(availableLength / 2)
  const endLength = Math.floor(availableLength / 2)
  return `${value.slice(0, startLength)}…${value.slice(-endLength)}`
}
