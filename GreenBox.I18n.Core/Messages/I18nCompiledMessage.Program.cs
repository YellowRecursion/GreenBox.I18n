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
