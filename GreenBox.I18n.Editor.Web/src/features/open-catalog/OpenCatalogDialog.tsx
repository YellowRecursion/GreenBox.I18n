import { useEffect, useState } from 'react'
import { FolderOpenOutlined } from '@ant-design/icons'
import { Alert, Button, Input, Modal } from 'antd'
import { layoutTokens } from '../../design/layoutTokens'
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
  const { state, openCatalog, dismissOperationError } = useCatalogSession()
  const resolvedIsOpen = isOpen ?? internalIsOpen
  const setOpen = (nextOpen: boolean) => {
    if (onOpenChange) {
      onOpenChange(nextOpen)
    } else {
      setInternalIsOpen(nextOpen)
    }
  }

  if (state.status !== 'ready') {
    return null
  }

  useEffect(() => {
    if (resolvedIsOpen) {
      setPath(catalogPath ?? '')
    }
  }, [resolvedIsOpen, catalogPath])

  const showDialog = () => {
    setPath(catalogPath ?? '')
    dismissOperationError()
    setOpen(true)
  }

  const closeDialog = () => {
    if (!state.isOpening) {
      dismissOperationError()
      setOpen(false)
    }
  }

  const submit = async () => {
    if (!path.trim() || state.isOpening) {
      return
    }

    if (await openCatalog(path)) {
      setOpen(false)
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
        okButtonProps={{ disabled: !path.trim() }}
        onOk={() => void submit()}
        onCancel={closeDialog}
      >
        <div style={{ paddingTop: layoutTokens.spacing.small }}>
          <Input
            autoFocus
            value={path}
            placeholder="C:\\Projects\\Game\\catalog.json"
            onChange={(event) => setPath(event.target.value)}
            onPressEnter={() => void submit()}
          />
          {state.operationError && (
            <Alert
              type="error"
              showIcon
              message={state.operationError}
              style={{ marginTop: layoutTokens.spacing.medium }}
            />
          )}
        </div>
      </Modal>
    </>
  )
}
