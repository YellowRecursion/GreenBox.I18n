import { StreamLanguage, type StreamParser } from '@codemirror/language'
import type { Extension } from '@codemirror/state'
import CodeEditor, { type CodeEditorProps } from './CodeEditor'

interface Mf2TokenizerState {}

const mf2Tokenizer: StreamParser<Mf2TokenizerState> = {
  name: 'MessageFormat 2',
  startState: () => ({}),
  token(stream) {
    if (stream.sol()) {
      stream.eatSpace()
      if (stream.match(/\.(?:input|local|match)\b/u)) {
        return 'keyword'
      }
      if (stream.match(/(?:\*|-?\d+|zero|one|two|few|many|other)(?=\s|\{\{)/u)) {
        return 'atom'
      }
    }

    if (stream.match(/\$[\p{L}_][\p{L}\p{N}_.-]*/u)) {
      return 'variableName'
    }
    if (stream.match(/:[\p{L}_][\p{L}\p{N}_.-]*/u)) {
      return 'typeName'
    }
    if (stream.match(/(?:\{\{|\}\}|\{|\})/u)) {
      return 'bracket'
    }
    if (stream.match(/(?:true|false)/u)) {
      return 'bool'
    }
    if (stream.match(/-?\d+(?:\.\d+)?/u)) {
      return 'number'
    }

    stream.next()
    return null
  },
}

const mf2Language: Extension = StreamLanguage.define(mf2Tokenizer)

export default function Mf2CodeEditor(props: Omit<CodeEditorProps, 'extensions'>) {
  return <CodeEditor {...props} extensions={mf2Language} />
}
