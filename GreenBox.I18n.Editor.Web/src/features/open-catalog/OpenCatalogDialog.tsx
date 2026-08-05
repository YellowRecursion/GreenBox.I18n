import { useState } from 'react'
import { FolderOpenOutlined } from '@ant-design/icons'
import { Alert, Button, Input, Modal } from 'antd'
import { layoutTokens } from '../../design/layoutTokens'
import { useCatalogSession } from '../../entities/catalog/model/useCatalogSession'

interface OpenCatalogDialogProps {
  catalogPath?: string
  initialOpen?: boolean
}

export function OpenCatalogDialog({ catalogPath, initialOpen = false }: OpenCatalogDialogProps) {
  const [isOpen, setIsOpen] = useState(initialOpen)
  const [path, setPath] = useState(catalogPath ?? '')
  const { state, openCatalog, dismissOperationError } = useCatalogSession()

  if (state.status !== 'ready') {
    return null
  }

  const showDialog = () => {
    setPath(catalogPath ?? '')
    dismissOperationError()
    setIsOpen(true)
  }

  const closeDialog = () => {
    if (!state.isOpening) {
      dismissOperationError()
      setIsOpen(false)
    }
  }

  const submit = async () => {
    if (!path.trim() || state.isOpening) {
      return
    }

    if (await openCatalog(path)) {
      setIsOpen(false)
    }
  }

  return (
    <>
      <Button
        type={catalogPath ? 'text' : 'primary'}
        icon={<FolderOpenOutlined />}
        onClick={showDialog}
      >
        {catalogPath ? 'Change' : 'Open catalog'}
      </Button>
      <Modal
        title={catalogPath ? 'Change catalog' : 'Open catalog'}
        open={isOpen}
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
