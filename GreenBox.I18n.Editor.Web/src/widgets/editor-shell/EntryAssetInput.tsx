import { useEffect, useRef, useState, type ClipboardEvent, type DragEvent } from 'react'
import { UploadOutlined } from '@ant-design/icons'
import { Button, Flex, Tooltip, Typography, Upload, theme, type UploadFile } from 'antd'
import {
  openUnityAsset,
  resolveDroppedUnityAsset,
  resolveUnityAssetReference,
  type ResolvedUnityAssetReference,
} from '../../entities/catalog/api/unityAssetReferences'
import type { CatalogAssetReference } from '../../entities/catalog/model/catalog'
import { layoutTokens } from '../../design/layoutTokens'

interface EntryAssetInputProps {
  asset: CatalogAssetReference | null
  onChange: (asset: CatalogAssetReference | null) => Promise<void>
}

interface ClipboardAssetReference extends CatalogAssetReference {
  format: string
  version: number
}

const clipboardFormat = 'greenbox.i18n.asset-reference'
const clipboardFormatVersion = 1

export function EntryAssetInput({ asset, onChange }: EntryAssetInputProps) {
  const { token } = theme.useToken()
  const [resolved, setResolved] = useState<ResolvedUnityAssetReference>()
  const [isDragging, setIsDragging] = useState(false)
  const [isChanging, setIsChanging] = useState(false)
  const [error, setError] = useState<string>()
  const isChangingRef = useRef(false)

  useEffect(() => {
    let isCurrent = true
    setResolved(undefined)
    setError(undefined)
    if (!asset) {
      return () => {
        isCurrent = false
      }
    }

    resolveUnityAssetReference(asset.assetGuid)
      .then((reference) => {
        if (isCurrent) {
          setResolved({ ...reference, localFileId: asset.localFileId })
        }
      })
      .catch((reason: unknown) => {
        if (isCurrent) {
          setError(reason instanceof Error ? reason.message : 'Unity asset could not be resolved.')
        }
      })

    return () => {
      isCurrent = false
    }
  }, [asset])

  const applyChange = async (change: () => Promise<void>, fallbackError: string) => {
    if (isChangingRef.current) {
      return
    }

    isChangingRef.current = true
    setIsChanging(true)
    setError(undefined)
    try {
      await change()
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : fallbackError)
    } finally {
      isChangingRef.current = false
      setIsChanging(false)
    }
  }

  const assignDroppedFile = async (file: File) => {
    await applyChange(async () => {
      const reference = await resolveDroppedUnityAsset(file.name)
      await onChange({
        assetGuid: reference.assetGuid,
        localFileId: reference.localFileId,
      })
    }, 'Dropped Unity asset could not be resolved.')
  }

  const pasteClipboard = async (text?: string) => {
    await applyChange(async () => {
      const clipboardText = text ?? await navigator.clipboard.readText()
      const reference = parseClipboardReference(clipboardText)
      await resolveUnityAssetReference(reference.assetGuid)
      await onChange(reference)
    }, 'Clipboard does not contain an i18n asset reference.')
  }

  const clear = async () => {
    await applyChange(async () => {
      await onChange(null)
    }, 'Unity asset reference could not be removed.')
  }

  const open = async () => {
    if (!asset) {
      return
    }

    setError(undefined)
    try {
      await openUnityAsset(asset.assetGuid)
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : 'Unity asset could not be opened.')
    }
  }

  const fileList: UploadFile[] = asset
    ? [{
        uid: `${asset.assetGuid}:${asset.localFileId ?? ''}`,
        name: resolved?.fileName ?? asset.assetGuid,
        status: 'done',
      }]
    : []

  return (
    <Flex vertical gap={layoutTokens.spacing.xSmall}>
      <div
        tabIndex={0}
        onPaste={(event: ClipboardEvent<HTMLDivElement>) => {
          event.preventDefault()
          void pasteClipboard(event.clipboardData.getData('text/plain'))
        }}
        onDragOver={(event: DragEvent<HTMLDivElement>) => {
          event.preventDefault()
          event.dataTransfer.dropEffect = 'copy'
          setIsDragging(true)
        }}
        onDragLeave={(event: DragEvent<HTMLDivElement>) => {
          if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
            setIsDragging(false)
          }
        }}
        onDrop={(event: DragEvent<HTMLDivElement>) => {
          event.preventDefault()
          setIsDragging(false)
          const files = [...event.dataTransfer.files]
          if (files.length !== 1) {
            setError('Drop exactly one asset from the Unity Project window.')
            return
          }

          void assignDroppedFile(files[0])
        }}
        style={{
          padding: isDragging ? layoutTokens.spacing.xSmall : 0,
          margin: isDragging ? -layoutTokens.spacing.xSmall : 0,
          borderRadius: token.borderRadius,
          background: isDragging ? token.colorFillSecondary : 'transparent',
          outline: 'none',
          transition: `background ${token.motionDurationFast}`,
        }}
      >
        {asset ? (
          <Upload
            fileList={fileList}
            maxCount={1}
            openFileDialogOnClick={false}
            onPreview={() => void open()}
            onRemove={() => {
              void clear()
              return false
            }}
            showUploadList={{
              showDownloadIcon: false,
              showPreviewIcon: true,
              showRemoveIcon: !isChanging,
            }}
            itemRender={(originNode) => (
              <Tooltip
                styles={{ container: { maxWidth: 480 } }}
                title={(
                  <Flex vertical gap={layoutTokens.spacing.xSmall}>
                    <div>
                      <strong>Path:</strong>{' '}
                      {resolved?.assetPath ?? 'Resolving Unity asset path...'}
                    </div>
                    <div>
                      <strong>GUID:</strong>{' '}
                      <code style={{ overflowWrap: 'anywhere' }}>{asset.assetGuid}</code>
                    </div>
                    <div>
                      <strong>Local File ID:</strong>{' '}
                      <code>{asset.localFileId ?? '[none]'}</code>
                    </div>
                  </Flex>
                )}
              >
                <div
                  onClick={(event) => {
                    if (!(event.target as HTMLElement).closest('a, button, [role="button"]')) {
                      void open()
                    }
                  }}
                >
                  {originNode}
                </div>
              </Tooltip>
            )}
          />
        ) : (
          <Upload.Dragger
            fileList={[]}
            maxCount={1}
            openFileDialogOnClick={false}
            showUploadList={false}
            styles={{ trigger: { padding: layoutTokens.spacing.xSmall } }}
            beforeUpload={(file, files) => {
              if (files.length !== 1) {
                setError('Drop exactly one asset from the Unity Project window.')
                return Upload.LIST_IGNORE
              }

              void assignDroppedFile(file)
              return Upload.LIST_IGNORE
            }}
          >
            <Flex align="center" justify="center" gap={layoutTokens.spacing.xSmall}>
              <Button
                type="text"
                size="small"
                loading={isChanging}
                icon={<UploadOutlined />}
                onClick={(event) => {
                  event.preventDefault()
                  event.stopPropagation()
                  void pasteClipboard()
                }}
              >
                Paste reference
              </Button>
              <Typography.Text type="secondary">or drop from Unity</Typography.Text>
            </Flex>
          </Upload.Dragger>
        )}
      </div>
      {error && <Typography.Text type="danger">{error}</Typography.Text>}
    </Flex>
  )
}

function parseClipboardReference(text: string): ClipboardAssetReference {
  const value = JSON.parse(text) as Partial<ClipboardAssetReference>
  if (value.format !== clipboardFormat ||
      value.version !== clipboardFormatVersion ||
      !isAssetGuid(value.assetGuid) ||
      (value.localFileId != null && !/^-?\d+$/.test(value.localFileId))) {
    throw new Error('Clipboard does not contain a supported i18n asset reference.')
  }

  return {
    format: value.format,
    version: value.version,
    assetGuid: value.assetGuid,
    localFileId: value.localFileId ?? null,
  }
}

function isAssetGuid(value: unknown): value is string {
  return typeof value === 'string' && /^[0-9a-f]{32}$/i.test(value)
}
