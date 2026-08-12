import { useEffect, useState } from 'react'
import { FolderOpenOutlined } from '@ant-design/icons'
import { Alert, Button, Input, Modal, Space } from 'antd'
import { layoutTokens } from '../../design/layoutTokens'
import { pickCatalogFile } from '../../entities/catalog/api/pickCatalogFile'
import { useCatalogSession } from '../../entities/catalog/model/useCatalogSession'

interface OpenCatalogDialogProps {
  catalogPath?: string
  initialOpen?: boolean
  isOpen?: boolean
  showButton?: boolean
  onOpenChange?: (isOpen: boolean) => void
}

export function OpenCatalogDialog({
  catalogPath,
  initialOpen = false,
  isOpen,
  showButton = true,
  onOpenChange,
}: OpenCatalogDialogProps) {
  const [internalIsOpen, setInternalIsOpen] = useState(initialOpen)
  const [path, setPath] = useState(catalogPath ?? '')
  const [isPicking, setIsPicking] = useState(false)
  const [selectionError, setSelectionError] = useState<string>()
  const { state, openCatalog, dismissOperationError } = useCatalogSession()
  const resolvedIsOpen = isOpen ?? internalIsOpen
  const setOpen = (nextOpen: boolean) => {
    if (onOpenChange) {
      onOpenChange(nextOpen)
    } else {
      setInternalIsOpen(nextOpen)
    }
  }

  useEffect(() => {
    if (resolvedIsOpen) {
      setPath(catalogPath ?? '')
      setSelectionError(undefined)
    }
  }, [resolvedIsOpen, catalogPath])

  if (state.status !== 'ready') {
    return null
  }

  const showDialog = () => {
    setPath(catalogPath ?? '')
    setSelectionError(undefined)
    dismissOperationError()
    setOpen(true)
  }

  const closeDialog = () => {
    if (!state.isOpening && !isPicking) {
      setSelectionError(undefined)
      dismissOperationError()
      setOpen(false)
    }
  }

  const submit = async () => {
    const normalizedPath = stripSurroundingQuotes(path)
    if (!normalizedPath || state.isOpening) {
      return
    }

    setPath(normalizedPath)
    setSelectionError(undefined)
    dismissOperationError()
    if (await openCatalog(normalizedPath)) {
      setOpen(false)
    }
  }

  const chooseCatalog = async () => {
    if (isPicking || state.isOpening) {
      return
    }

    setIsPicking(true)
    setSelectionError(undefined)
    dismissOperationError()
    try {
      const path = await pickCatalogFile()
      if (path) {
        setPath(path)
      }
    } catch (error: unknown) {
      setSelectionError(error instanceof Error ? error.message : 'Catalog could not be selected.')
    } finally {
      setIsPicking(false)
    }
  }

  return (
    <>
      {showButton ? (
        <Button
          type={catalogPath ? 'text' : 'primary'}
          icon={<FolderOpenOutlined />}
          onClick={showDialog}
        >
          Open
        </Button>
      ) : null}
      <Modal
        title="Open catalog"
        open={resolvedIsOpen}
        okText="Open"
        cancelText="Cancel"
        confirmLoading={state.isOpening}
        okButtonProps={{ disabled: !path.trim() || isPicking }}
        closable={!state.isOpening && !isPicking}
        maskClosable={!state.isOpening && !isPicking}
        onOk={() => void submit()}
        onCancel={closeDialog}
      >
        <div style={{ paddingTop: layoutTokens.spacing.small }}>
          <Space.Compact block>
            <Input
              autoFocus
              value={path}
              placeholder="C:\\Projects\\Game\\localization.json"
              onChange={(event) => setPath(stripSurroundingQuotes(event.target.value))}
              onPressEnter={() => void submit()}
            />
            <Button
              aria-label="Choose catalog"
              title="Choose catalog"
              loading={isPicking}
              disabled={state.isOpening}
              icon={<FolderOpenOutlined />}
              onClick={() => void chooseCatalog()}
            />
          </Space.Compact>
          {(selectionError || state.operationError) && (
            <Alert
              type="error"
              showIcon
              message={selectionError ?? state.operationError}
              style={{ marginTop: layoutTokens.spacing.medium }}
            />
          )}
        </div>
      </Modal>
    </>
  )
}

function stripSurroundingQuotes(value: string) {
  const trimmed = value.trim()
  if (trimmed.length >= 2) {
    const firstCharacter = trimmed[0]
    const lastCharacter = trimmed[trimmed.length - 1]
    if ((firstCharacter === '"' && lastCharacter === '"') ||
        (firstCharacter === "'" && lastCharacter === "'")) {
      return trimmed.slice(1, -1)
    }
  }

  return trimmed
}
