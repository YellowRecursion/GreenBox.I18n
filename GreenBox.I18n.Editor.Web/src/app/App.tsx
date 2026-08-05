import { AppProviders } from './providers/AppProviders'
import { EditorShell } from '../widgets/editor-shell/EditorShell'

export function App() {
  return (
    <AppProviders>
      <EditorShell />
    </AppProviders>
  )
}
