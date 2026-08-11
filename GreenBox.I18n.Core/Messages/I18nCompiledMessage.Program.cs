using System;
using System.Globalization;

namespace GreenBox.I18n
{
    public sealed partial class I18nCompiledMessage
    {
        internal readonly struct MessagePart
        {
            public MessagePart(
                MessagePartKind kind,
                string value,
                int sourcePosition,
                NumberOptions numberOptions = default)
            {
                Kind = kind;
                Value = value;
                SourcePosition = sourcePosition;
                NumberOptions = numberOptions;
            }

            public MessagePartKind Kind { get; }

            public string Value { get; }

            public int SourcePosition { get; }
            public NumberOptions NumberOptions { get; }
        }

        internal enum MessagePartKind
        {
            Text,
            Variable,
            NumberVariable,
        }

        internal sealed class MessageMatcher
        {
            private readonly MessageSelector[] _selectors;
            private readonly MessageVariant[] _variants;

            public MessageMatcher(MessageSelector[] selectors, MessageVariant[] variants)
            {
                _selectors = selectors;
                _variants = variants;
            }

            internal MessageSelector[] Selectors => _selectors;

            internal MessageVariant[] Variants => _variants;

            public bool TrySelect<TArguments>(
                CultureInfo culture,
                TArguments arguments,
                out MessagePart[] parts,
                out I18nMessageDiagnostic diagnostic)
                where TArguments : struct, IArgumentSource
            {
                for (int selectorIndex = 0; selectorIndex < _selectors.Length; selectorIndex++)
                {
                    MessageSelector selector = _selectors[selectorIndex];
                    bool found = selector.Kind == MessageSelectorKind.String
                        ? arguments.TryGetString(selector.Name, out _)
                        : arguments.TryGetNumber(selector.Name, out _);
                    if (!found)
                    {
                        parts = Array.Empty<MessagePart>();
                        diagnostic = new I18nMessageDiagnostic(
                            I18nMessageDiagnosticCodes.MissingArgument,
                            $"Selector argument '{selector.Name}' was not supplied or has an invalid value.",
                            -1,
                            selector.Name);
                        return false;
                    }

                    if (selector.Kind != MessageSelectorKind.String &&
                        arguments.TryGetNumber(selector.Name, out decimal number) &&
                        !selector.NumberOptions.TryPrepare(number, out _))
                    {
                        parts = Array.Empty<MessagePart>();
                        diagnostic = UnsupportedNumberDiagnostic(selector.Name, -1);
                        return false;
                    }
                }

                int bestVariant = -1;
                for (int variantIndex = 0; variantIndex < _variants.Length; variantIndex++)
                {
                    if (!Matches(_variants[variantIndex], culture, arguments))
                    {
                        continue;
                    }

                    if (bestVariant < 0 ||
                        IsMoreSpecific(_variants[variantIndex], _variants[bestVariant], culture, arguments))
                    {
                        bestVariant = variantIndex;
                    }
                }

                if (bestVariant < 0)
                {
                    throw new InvalidOperationException("A compiled matcher must have a matching wildcard variant.");
                }

                parts = _variants[bestVariant].Parts;
                diagnostic = default;
                return true;
            }

            private bool Matches<TArguments>(
                MessageVariant variant,
                CultureInfo culture,
                TArguments arguments)
                where TArguments : struct, IArgumentSource
            {
                for (int selectorIndex = 0; selectorIndex < _selectors.Length; selectorIndex++)
                {
                    if (GetRank(_selectors[selectorIndex], variant.Keys[selectorIndex], culture, arguments) < 0)
                    {
                        return false;
                    }
                }

                return true;
            }

            private bool IsMoreSpecific<TArguments>(
                MessageVariant candidate,
                MessageVariant current,
                CultureInfo culture,
                TArguments arguments)
                where TArguments : struct, IArgumentSource
            {
                for (int selectorIndex = 0; selectorIndex < _selectors.Length; selectorIndex++)
                {
                    int candidateRank = GetRank(
                        _selectors[selectorIndex], candidate.Keys[selectorIndex], culture, arguments);
                    int currentRank = GetRank(
                        _selectors[selectorIndex], current.Keys[selectorIndex], culture, arguments);
                    if (candidateRank != currentRank)
                    {
                        return candidateRank < currentRank;
                    }
                }

                return false;
            }

            private static int GetRank<TArguments>(
                MessageSelector selector,
                MessageVariantKey key,
                CultureInfo culture,
                TArguments arguments)
                where TArguments : struct, IArgumentSource
            {
                if (key.Kind == MessageVariantKeyKind.Wildcard)
                {
                    return selector.Kind == MessageSelectorKind.String ? 1 : 2;
                }

                if (selector.Kind == MessageSelectorKind.String)
                {
                    arguments.TryGetString(selector.Name, out string value);
                    return I18nUnicode.EqualsNfc(key.Value, value) ? 0 : -1;
                }

                arguments.TryGetNumber(selector.Name, out decimal number);
                if (!selector.NumberOptions.TryPrepare(number, out number))
                {
                    return -1;
                }
                if (key.Kind == MessageVariantKeyKind.ExactNumber)
                {
                    return key.ExactNumber == number ? 0 : -1;
                }

                string category = I18nPluralRules.Select(
                    culture,
                    number,
                    selector.Kind == MessageSelectorKind.OrdinalNumber);
                return string.Equals(key.Value, category, StringComparison.Ordinal) ? 1 : -1;
            }
        }

        internal readonly struct MessageSelector
        {
            public MessageSelector(string name, MessageSelectorKind kind, NumberOptions numberOptions)
            {
                Name = name;
                Kind = kind;
                NumberOptions = numberOptions;
            }

            public string Name { get; }

            public MessageSelectorKind Kind { get; }
            public NumberOptions NumberOptions { get; }
        }

        internal readonly struct MessageVariant
        {
            public MessageVariant(MessageVariantKey[] keys, MessagePart[] parts)
            {
                Keys = keys;
                Parts = parts;
            }

            public MessageVariantKey[] Keys { get; }

            public MessagePart[] Parts { get; }
        }

        internal readonly struct MessageVariantKey
        {
            public MessageVariantKey(MessageVariantKeyKind kind, string value, decimal exactNumber)
            {
                Kind = kind;
                Value = value;
                ExactNumber = exactNumber;
            }

            public MessageVariantKeyKind Kind { get; }

            public string Value { get; }

            public decimal ExactNumber { get; }
        }

        internal enum MessageVariantKeyKind
        {
            Wildcard,
            ExactNumber,
            Category,
            String,
        }

        internal enum MessageSelectorKind
        {
            CardinalNumber,
            OrdinalNumber,
            ExactNumber,
            String,
        }
    }
}
