import { useEffect, useMemo, useRef } from 'react'
import { closeBrackets, closeBracketsKeymap } from '@codemirror/autocomplete'
import { defaultKeymap, history, historyKeymap } from '@codemirror/commands'
import { bracketMatching, syntaxHighlighting } from '@codemirror/language'
import { closeSearchPanel, searchKeymap, searchPanelOpen } from '@codemirror/search'
import { EditorState, type Extension } from '@codemirror/state'
import {
  color as oneDarkColor,
  oneDarkHighlightStyle,
  oneDarkTheme,
} from '@codemirror/theme-one-dark'
import {
  drawSelection,
  EditorView,
  highlightSpecialChars,
  keymap,
  lineNumbers,
  placeholder as editorPlaceholder,
} from '@codemirror/view'
import { theme } from 'antd'

export interface CodeEditorProps {
  value: string
  ariaLabel: string
  placeholder?: string
  disabled?: boolean
  status?: 'error'
  autoFocus?: boolean
  extensions?: Extension
  onChange: (value: string) => void
  onEscape?: () => void
}

export default function CodeEditor({
  value,
  ariaLabel,
  placeholder,
  disabled = false,
  status,
  autoFocus = false,
  extensions = [],
  onChange,
  onEscape,
}: CodeEditorProps) {
  const { token } = theme.useToken()
  const parentRef = useRef<HTMLDivElement>(null)
  const viewRef = useRef<EditorView>(null)
  const onChangeRef = useRef(onChange)
  const onEscapeRef = useRef(onEscape)
  const synchronizingRef = useRef(false)
  const valueRef = useRef(value)

  onChangeRef.current = onChange
  onEscapeRef.current = onEscape
  valueRef.current = value

  const editorTheme = useMemo(() => EditorView.theme({
    '&': {
      height: '100%',
      minHeight: '0',
      overflow: 'hidden',
      color: token.colorText,
      backgroundColor: 'transparent',
      border: `1px solid ${status === 'error' ? token.colorError : token.colorBorder}`,
      borderRadius: `${token.borderRadius}px`,
      fontSize: `${token.fontSize}px`,
      opacity: disabled ? '0.65' : '1',
    },
    '&.cm-focused': {
      outline: 'none',
      borderColor: status === 'error' ? token.colorError : token.colorTextQuaternary,
      boxShadow: status === 'error'
        ? `0 0 0 2px ${token.colorErrorBg}`
        : 'none',
    },
    '.cm-scroller': {
      minHeight: '0',
      overflow: 'auto',
      fontFamily: token.fontFamilyCode,
      lineHeight: String(token.lineHeight),
    },
    '.cm-content': {
      minHeight: '100%',
      padding: `${token.paddingXS}px 0`,
      caretColor: oneDarkColor.cursor,
    },
    '.cm-cursor, .cm-dropCursor': {
      borderLeftColor: oneDarkColor.cursor,
      borderLeftWidth: '2px',
    },
    '.cm-line': {
      padding: `0 ${token.paddingSM}px`,
    },
    '.cm-gutters': {
      color: token.colorTextQuaternary,
      backgroundColor: 'transparent',
      borderRight: `1px solid ${token.colorSplit}`,
    },
    '.cm-placeholder': {
      color: token.colorTextPlaceholder,
      fontStyle: 'normal',
    },
    '.cm-panels': {
      color: token.colorTextSecondary,
      backgroundColor: token.colorBgElevated,
    },
    '.cm-panel.cm-search': {
      display: 'flex',
      alignItems: 'center',
      gap: `${token.marginXXS}px`,
      flexWrap: 'wrap',
      padding: `${token.paddingXS}px ${token.paddingLG}px ${token.paddingXS}px ${token.paddingXS}px`,
      borderTop: `1px solid ${token.colorSplit}`,
      fontFamily: token.fontFamily,
    },
    '.cm-panel.cm-search br': {
      flexBasis: '100%',
      width: '0',
      height: '0',
    },
    '.cm-panel.cm-search input[type=text]': {
      height: `${token.controlHeightSM}px`,
      padding: `0 ${token.paddingXS}px`,
      color: token.colorText,
      backgroundColor: token.colorBgContainer,
      border: `1px solid ${token.colorBorder}`,
      borderRadius: `${token.borderRadiusSM}px`,
      outline: 'none',
      font: 'inherit',
    },
    '.cm-panel.cm-search input[type=text]:focus': {
      borderColor: token.colorTextQuaternary,
      boxShadow: 'none',
    },
    '.cm-panel.cm-search input[name=search], .cm-panel.cm-search input[name=replace]': {
      width: '180px',
    },
    '.cm-panel.cm-search .cm-button': {
      height: `${token.controlHeightSM}px`,
      padding: `0 ${token.paddingXS}px`,
      color: token.colorTextSecondary,
      background: token.colorFillTertiary,
      border: `1px solid ${token.colorBorder}`,
      borderRadius: `${token.borderRadiusSM}px`,
      font: 'inherit',
      cursor: 'pointer',
    },
    '.cm-panel.cm-search .cm-button:hover': {
      color: token.colorText,
      background: token.colorFillSecondary,
      borderColor: token.colorTextQuaternary,
    },
    '.cm-panel.cm-search label': {
      display: 'inline-flex',
      alignItems: 'center',
      gap: `${token.marginXXS}px`,
      margin: `0 ${token.marginXS}px 0 0`,
      color: token.colorTextSecondary,
      fontSize: `${token.fontSizeSM}px`,
    },
    '.cm-panel.cm-search input[type=checkbox]': {
      width: '14px',
      height: '14px',
      margin: '0',
      accentColor: oneDarkColor.malibu,
    },
    '.cm-panel.cm-search button[name=close]': {
      top: `${token.paddingXS}px`,
      right: `${token.paddingXS}px`,
      width: `${token.controlHeightSM}px`,
      height: `${token.controlHeightSM}px`,
      color: token.colorTextSecondary,
      background: 'transparent',
      borderRadius: `${token.borderRadiusSM}px`,
      cursor: 'pointer',
    },
    '.cm-panel.cm-search button[name=close]:hover': {
      color: token.colorText,
      background: token.colorFillTertiary,
    },
    '.cm-searchMatch': {
      backgroundColor: `${oneDarkColor.whiskey}35`,
      outline: `1px solid ${oneDarkColor.whiskey}66`,
    },
    '.cm-searchMatch-selected': {
      backgroundColor: `${oneDarkColor.whiskey}66`,
    },
  }), [disabled, status, token])

  useEffect(() => {
    const parent = parentRef.current
    if (parent === null) {
      return
    }

    const state = EditorState.create({
      doc: valueRef.current,
      extensions: [
        lineNumbers(),
        highlightSpecialChars(),
        history(),
        drawSelection(),
        bracketMatching(),
        closeBrackets(),
        oneDarkTheme,
        syntaxHighlighting(oneDarkHighlightStyle),
        EditorView.lineWrapping,
        EditorView.contentAttributes.of({ 'aria-label': ariaLabel }),
        EditorView.updateListener.of((update) => {
          if (update.docChanged && !synchronizingRef.current) {
            onChangeRef.current(update.state.doc.toString())
          }
        }),
        EditorView.domEventHandlers({
          keydown(event, view) {
            if (event.key !== 'Escape' || event.isComposing) {
              return false
            }

            event.preventDefault()
            event.stopPropagation()
            if (searchPanelOpen(view.state)) {
              closeSearchPanel(view)
              return true
            }

            onEscapeRef.current?.()
            return true
          },
        }),
        keymap.of([
          ...closeBracketsKeymap,
          ...defaultKeymap,
          ...searchKeymap,
          ...historyKeymap,
        ]),
        placeholder ? editorPlaceholder(placeholder) : [],
        extensions,
        editorTheme,
      ],
    })
    const view = new EditorView({ state, parent })
    viewRef.current = view
    view.contentDOM.contentEditable = disabled ? 'false' : 'true'

    const closeSearchBeforeModal = (event: globalThis.KeyboardEvent) => {
      if (event.key !== 'Escape' || event.isComposing || !searchPanelOpen(view.state)) {
        return
      }

      event.preventDefault()
      event.stopImmediatePropagation()
      closeSearchPanel(view)
    }
    parent.addEventListener('keydown', closeSearchBeforeModal, true)

    if (autoFocus) {
      requestAnimationFrame(() => {
        if (viewRef.current !== view) {
          return
        }

        view.dispatch({ selection: { anchor: view.state.doc.length } })
        view.focus()
      })
    }

    return () => {
      parent.removeEventListener('keydown', closeSearchBeforeModal, true)
      viewRef.current = null
      view.destroy()
    }
  }, [ariaLabel, autoFocus, disabled, editorTheme, extensions, placeholder])

  useEffect(() => {
    const view = viewRef.current
    if (view === null) {
      return
    }

    const currentValue = view.state.doc.toString()
    if (currentValue === value) {
      return
    }

    synchronizingRef.current = true
    try {
      view.dispatch({
        changes: { from: 0, to: currentValue.length, insert: value },
      })
    } finally {
      synchronizingRef.current = false
    }
  }, [value])

  return <div ref={parentRef} style={{ flex: 1, minHeight: 0, overflow: 'hidden' }} />
}
