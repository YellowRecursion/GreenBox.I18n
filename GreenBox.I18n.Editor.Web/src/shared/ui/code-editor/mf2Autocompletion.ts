import {
  autocompletion,
  type Completion,
  type CompletionContext,
  type CompletionResult,
} from '@codemirror/autocomplete'

const identifier = '[\\p{L}_][\\p{L}\\p{N}_.-]*'
const identifierSuffix = '[\\p{L}\\p{N}_.-]*'

const directiveOptions: readonly Completion[] = [
  {
    label: '.input',
    type: 'keyword',
    detail: 'external argument',
    info: 'Declares a named value supplied by game code.',
  },
  {
    label: '.local',
    type: 'keyword',
    detail: 'derived value',
    info: 'Declares a value derived from another message variable.',
  },
  {
    label: '.match',
    type: 'keyword',
    detail: 'select variant',
    info: 'Chooses a message variant using one or more declared values.',
  },
]

const typeOptions: readonly Completion[] = [
  {
    label: ':string',
    type: 'type',
    detail: 'text',
    info: 'Treats the argument as text and enables string matching.',
  },
  {
    label: ':number',
    type: 'type',
    detail: 'localized number',
    info: 'Formats a number for the active locale and enables plural matching.',
  },
  {
    label: ':integer',
    type: 'type',
    detail: 'whole number',
    info: 'Formats a number without fractional digits.',
  },
  {
    label: ':percent',
    type: 'type',
    detail: 'localized percent',
    info: 'Formats a numeric ratio as a locale-aware percentage.',
  },
]

const offsetOption: Completion = {
  label: ':offset',
  type: 'type',
  detail: 'add or subtract',
  info: 'Derives a local numeric value by applying an integer offset.',
}

const variantOptions: readonly Completion[] = [
  ['zero', 'plural category'],
  ['one', 'plural category'],
  ['two', 'plural category'],
  ['few', 'plural category'],
  ['many', 'plural category'],
  ['other', 'plural category'],
  ['*', 'required fallback'],
].map(([label, detail]) => ({ label, detail, type: label === '*' ? 'keyword' : 'enum' }))

export const mf2Autocompletion = autocompletion({
  override: [completeMf2],
  activateOnTyping: true,
  maxRenderedOptions: 40,
})

function completeMf2(context: CompletionContext): CompletionResult | null {
  const line = context.state.doc.lineAt(context.pos)
  const lineBeforeCursor = context.state.sliceDoc(line.from, context.pos)

  const directive = lineBeforeCursor.match(/^\s*(\.[\p{L}]*)$/u)
  if (directive !== null) {
    return completion(directive, context.pos, directiveOptions, /\.[\p{L}]*/u)
  }

  const variable = lineBeforeCursor.match(new RegExp(`(\\$${identifierSuffix})$`, 'u'))
  if (variable !== null) {
    const names = findDeclaredVariables(context.state.doc.toString())
    if (names.length === 0) {
      return null
    }

    return completion(
      variable,
      context.pos,
      names.map((name) => ({
        label: `$${name}`,
        type: 'variable',
        detail: 'declared argument',
      })),
      /\$[\p{L}\p{N}_.-]*/u,
    )
  }

  const annotation = lineBeforeCursor.match(/(:[\p{L}]*)$/u)
  if (annotation !== null) {
    const options = /^\s*\.local\b/u.test(line.text)
      ? [...typeOptions, offsetOption]
      : typeOptions
    return completion(annotation, context.pos, options, /:[\p{L}]*/u)
  }

  const variant = lineBeforeCursor.match(/^\s*([\p{L}*]*)$/u)
  if (variant !== null && hasMatcherBefore(context.state.doc.toString(), line.from)) {
    if (!context.explicit && variant[1].length === 0) {
      return null
    }

    return completion(variant, context.pos, variantOptions, /[\p{L}*]*/u)
  }

  return null
}

function completion(
  match: RegExpMatchArray,
  position: number,
  options: readonly Completion[],
  validFor: RegExp,
): CompletionResult {
  return {
    from: position - match[1].length,
    options,
    validFor,
  }
}

function findDeclaredVariables(source: string): string[] {
  const names = new Set<string>()
  const declaration = new RegExp(
    `^\\s*\\.(?:input\\s+\\{\\s*|local\\s+)\\$(${identifier})`,
    'gmu',
  )

  for (const match of source.matchAll(declaration)) {
    names.add(match[1].normalize('NFC'))
  }

  return [...names]
}

function hasMatcherBefore(source: string, lineStart: number): boolean {
  return /^\s*\.match\b/mu.test(source.slice(0, lineStart))
}
