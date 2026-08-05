import { useState } from 'react'
import { Alert, Button, Flex, Input } from 'antd'
import { layoutTokens } from '../../design/layoutTokens'
import { useCatalogSession } from '../../entities/catalog/model/useCatalogSession'

export function OpenCatalogForm() {
  const [path, setPath] = useState('')
  const { state, openCatalog } = useCatalogSession()

  if (state.status !== 'ready') {
    return null
  }

  const submit = () => {
    if (!state.isOpening) {
      void openCatalog(path)
    }
  }

  return (
    <Flex vertical gap={layoutTokens.spacing.small} style={{ width: 560, maxWidth: '100%' }}>
      <Flex gap={layoutTokens.spacing.small}>
        <Input
          value={path}
          placeholder="C:\\Projects\\Game\\catalog.json"
          onChange={(event) => setPath(event.target.value)}
          onPressEnter={submit}
        />
        <Button type="primary" loading={state.isOpening} onClick={submit}>
          Open catalog
        </Button>
      </Flex>
      {state.operationError && <Alert type="error" showIcon message={state.operationError} />}
    </Flex>
  )
}
