import { CopyOutlined, DownOutlined, ExportOutlined, FolderOpenOutlined, RedoOutlined, SaveOutlined, TranslationOutlined, UndoOutlined } from '@ant-design/icons'
import { useCallback, useEffect, useState } from 'react'
import { Badge, Button, Dropdown, Flex, Modal, Space, Tooltip, Typography, message, theme, type MenuProps } from 'antd'
import { layoutTokens } from '../../design/layoutTokens'
import { OpenCatalogDialog } from '../../features/open-catalog/OpenCatalogDialog'
import { HttpError } from '../../shared/api/httpClient'
import type { CatalogSourceStatus } from '../../entities/catalog/api/getCatalogSourceStatus'
import { openCatalogFile } from '../../entities/catalog/api/openCatalogFile'
import type { UnityProjectStatus } from '../../entities/unity-project/api/getUnityProjectStatus'
import unityGameEngineIcon from '../../assets/unity-game-engine-icon.webp'

interface EditorHeaderProps {
  catalogPath: string
  isDirty: boolean
  sourceStatus: CatalogSourceStatus
  unityProject?: UnityProjectStatus
  undoLabel?: string
  redoLabel?: string
  historyDirection?: 'undo' | 'redo'
  onUndo(): Promise<void>
  onRedo(): Promise<void>
  onSave(overwriteExternalChanges?: boolean): Promise<void>
  onRevert(): Promise<void>
}

export function EditorHeader({
  catalogPath,
  isDirty,
  sourceStatus,
  unityProject,
  undoLabel,
  redoLabel,
  historyDirection,
  onUndo,
  onRedo,
  onSave,
  onRevert,
}: EditorHeaderProps) {
  const { token } = theme.useToken()
  const [isSaving, setIsSaving] = useState(false)
  const [isReverting, setIsReverting] = useState(false)
  const [hasExternalConflict, setHasExternalConflict] = useState(false)
  const [isOpenCatalogDialogOpen, setIsOpenCatalogDialogOpen] = useState(false)
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

  const applyHistory = useCallback(async (direction: 'undo' | 'redo') => {
    try {
      await (direction === 'undo' ? onUndo() : onRedo())
    } catch (error: unknown) {
      messageApi.error(error instanceof Error ? error.message : 'History operation failed.')
    }
  }, [messageApi, onRedo, onUndo])

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.code === 'KeyS') {
        event.preventDefault()
        if (isDirty && !isSaving) {
          void save()
        }
        return
      }

      if (!(event.ctrlKey || event.metaKey) || isTextEditingTarget(event.target)) {
        return
      }

      const wantsRedo = event.code === 'KeyY' || event.code === 'KeyZ' && event.shiftKey
      const wantsUndo = event.code === 'KeyZ' && !event.shiftKey
      if (wantsUndo && undoLabel && !historyDirection) {
        event.preventDefault()
        void applyHistory('undo')
      } else if (wantsRedo && redoLabel && !historyDirection) {
        event.preventDefault()
        void applyHistory('redo')
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [applyHistory, historyDirection, isDirty, isSaving, redoLabel, save, undoLabel])

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

  const openCatalogPath = async () => {
    try {
      await openCatalogFile()
    } catch (error: unknown) {
      messageApi.error(error instanceof Error ? error.message : 'Could not open file.')
    }
  }

  const copyCatalogPath = async () => {
    try {
      await navigator.clipboard.writeText(catalogPath)
      messageApi.success('Catalog path copied.')
    } catch {
      messageApi.error('Copy failed.')
    }
  }

  const pathMenu: MenuProps = {
    items: [
      {
        key: 'open-catalog',
        icon: <FolderOpenOutlined />,
        label: 'Change catalog',
      },
      {
        key: 'open-file',
        icon: <ExportOutlined />,
        label: 'Open file',
      },
      {
        key: 'copy-path',
        icon: <CopyOutlined />,
        label: 'Copy path',
      },
    ],
    onClick: ({ key }) => {
      if (key === 'open-catalog') {
        setIsOpenCatalogDialogOpen(true)
      } else if (key === 'open-file') {
        void openCatalogPath()
      } else if (key === 'copy-path') {
        void copyCatalogPath()
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

      <Flex align="center" gap={layoutTokens.spacing.small} style={{ minWidth: 0, flex: '0 1 auto' }}>
        <Flex align="center" gap={layoutTokens.spacing.xSmall} style={{ minWidth: 0, flex: '0 1 auto' }}>
          <Dropdown menu={pathMenu} trigger={['click']}>
            <Typography.Text
              type="secondary"
              ellipsis={{ tooltip: catalogPath }}
              style={{
                direction: 'rtl',
                textAlign: 'left',
                minWidth: 0,
                whiteSpace: 'nowrap',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
                cursor: 'pointer',
              }}
            >
              {catalogPath}
            </Typography.Text>
          </Dropdown>
        </Flex>

        {unityProject?.isUnityProject && (
          <Tooltip
            title={(
              <Flex vertical gap={2}>
                <Typography.Text strong>{unityProject.projectName}</Typography.Text>
                <Typography.Text type="secondary">{unityProject.projectPath}</Typography.Text>
                <Typography.Text>
                  {unityProject.isEditorOnline
                    ? 'Unity Editor is running.'
                    : 'Unity Editor is not running.'}
                </Typography.Text>
              </Flex>
            )}
          >
            <Flex
              align="center"
              gap={layoutTokens.spacing.xSmall}
              style={{ cursor: 'help', flex: '0 0 auto' }}
            >
              <span
                aria-hidden
                style={{
                  display: 'block',
                  width: 16,
                  height: 16,
                  backgroundColor: token.colorTextSecondary,
                  mask: `url(${unityGameEngineIcon}) center / contain no-repeat`,
                  WebkitMask: `url(${unityGameEngineIcon}) center / contain no-repeat`,
                }}
              />
              <Typography.Text type="secondary">{unityProject.projectName}</Typography.Text>
              <Badge
                status={unityProject.isEditorOnline ? 'success' : 'default'}
                aria-label={unityProject.isEditorOnline ? 'Online' : 'Offline'}
              />
            </Flex>
          </Tooltip>
        )}
      </Flex>
      <OpenCatalogDialog
        catalogPath={catalogPath}
        isOpen={isOpenCatalogDialogOpen}
        onOpenChange={setIsOpenCatalogDialogOpen}
        showButton={false}
      />

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
        <Tooltip title={undoLabel ? `Undo ${undoLabel} (Ctrl+Z)` : 'Nothing to undo'}>
          <Button
            icon={<UndoOutlined />}
            aria-label={undoLabel ? `Undo ${undoLabel}` : 'Undo'}
            disabled={!undoLabel || Boolean(historyDirection)}
            loading={historyDirection === 'undo'}
            onClick={() => void applyHistory('undo')}
          />
        </Tooltip>
        <Tooltip title={redoLabel ? `Redo ${redoLabel} (Ctrl+Shift+Z)` : 'Nothing to redo'}>
          <Button
            icon={<RedoOutlined />}
            aria-label={redoLabel ? `Redo ${redoLabel}` : 'Redo'}
            disabled={!redoLabel || Boolean(historyDirection)}
            loading={historyDirection === 'redo'}
            onClick={() => void applyHistory('redo')}
          />
        </Tooltip>
      </Space.Compact>

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

function isTextEditingTarget(target: EventTarget | null) {
  return target instanceof HTMLInputElement ||
    target instanceof HTMLTextAreaElement ||
    target instanceof HTMLSelectElement ||
    target instanceof HTMLElement && target.isContentEditable
}

