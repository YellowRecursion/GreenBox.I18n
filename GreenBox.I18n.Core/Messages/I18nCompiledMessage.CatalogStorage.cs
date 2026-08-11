using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GreenBox.I18n
{
    public sealed partial class I18nCompiledMessage
    {
        internal static I18nMessageFormatResult FormatStored(
            I18nCompiledCatalogStorage storage,
            int message,
            CultureInfo culture)
        {
            return FormatStoredCore(storage, message, culture, new NoArguments());
        }

        internal static I18nMessageFormatResult FormatStored<T1>(
            I18nCompiledCatalogStorage storage,
            int message,
            CultureInfo culture,
            (string Name, T1 Value) argument1)
        {
            return FormatStoredCore(
                storage,
                message,
                culture,
                new OneArgument<T1>(argument1.Name, argument1.Value));
        }

        internal static I18nMessageFormatResult FormatStored<T1, T2>(
            I18nCompiledCatalogStorage storage,
            int message,
            CultureInfo culture,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2)
        {
            return FormatStoredCore(
                storage,
                message,
                culture,
                new TwoArguments<T1, T2>(argument1.Name, argument1.Value, argument2.Name, argument2.Value));
        }

        internal static I18nMessageFormatResult FormatStored<T1, T2, T3>(
            I18nCompiledCatalogStorage storage,
            int message,
            CultureInfo culture,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3)
        {
            return FormatStoredCore(
                storage,
                message,
                culture,
                Combine(
                    new TwoArguments<T1, T2>(argument1.Name, argument1.Value, argument2.Name, argument2.Value),
                    new OneArgument<T3>(argument3.Name, argument3.Value)));
        }

        internal static I18nMessageFormatResult FormatStored<T1, T2, T3, T4>(
            I18nCompiledCatalogStorage storage,
            int message,
            CultureInfo culture,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4)
        {
            return FormatStoredCore(
                storage,
                message,
                culture,
                Combine(
                    new TwoArguments<T1, T2>(argument1.Name, argument1.Value, argument2.Name, argument2.Value),
                    new TwoArguments<T3, T4>(argument3.Name, argument3.Value, argument4.Name, argument4.Value)));
        }

        internal static I18nMessageFormatResult FormatStored<T1, T2, T3, T4, T5>(
            I18nCompiledCatalogStorage storage,
            int message,
            CultureInfo culture,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5)
        {
            var firstFour = Combine(
                new TwoArguments<T1, T2>(argument1.Name, argument1.Value, argument2.Name, argument2.Value),
                new TwoArguments<T3, T4>(argument3.Name, argument3.Value, argument4.Name, argument4.Value));
            return FormatStoredCore(
                storage,
                message,
                culture,
                Combine(firstFour, new OneArgument<T5>(argument5.Name, argument5.Value)));
        }

        internal static I18nMessageFormatResult FormatStored<T1, T2, T3, T4, T5, T6>(
            I18nCompiledCatalogStorage storage,
            int message,
            CultureInfo culture,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5,
            (string Name, T6 Value) argument6)
        {
            var firstFour = Combine(
                new TwoArguments<T1, T2>(argument1.Name, argument1.Value, argument2.Name, argument2.Value),
                new TwoArguments<T3, T4>(argument3.Name, argument3.Value, argument4.Name, argument4.Value));
            return FormatStoredCore(
                storage,
                message,
                culture,
                Combine(
                    firstFour,
                    new TwoArguments<T5, T6>(argument5.Name, argument5.Value, argument6.Name, argument6.Value)));
        }

        internal static I18nMessageFormatResult FormatStored<T1, T2, T3, T4, T5, T6, T7>(
            I18nCompiledCatalogStorage storage,
            int message,
            CultureInfo culture,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5,
            (string Name, T6 Value) argument6,
            (string Name, T7 Value) argument7)
        {
            var firstFour = Combine(
                new TwoArguments<T1, T2>(argument1.Name, argument1.Value, argument2.Name, argument2.Value),
                new TwoArguments<T3, T4>(argument3.Name, argument3.Value, argument4.Name, argument4.Value));
            var lastThree = Combine(
                new TwoArguments<T5, T6>(argument5.Name, argument5.Value, argument6.Name, argument6.Value),
                new OneArgument<T7>(argument7.Name, argument7.Value));
            return FormatStoredCore(storage, message, culture, Combine(firstFour, lastThree));
        }

        internal static I18nMessageFormatResult FormatStored<T1, T2, T3, T4, T5, T6, T7, T8>(
            I18nCompiledCatalogStorage storage,
            int message,
            CultureInfo culture,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5,
            (string Name, T6 Value) argument6,
            (string Name, T7 Value) argument7,
            (string Name, T8 Value) argument8)
        {
            var firstFour = Combine(
                new TwoArguments<T1, T2>(argument1.Name, argument1.Value, argument2.Name, argument2.Value),
                new TwoArguments<T3, T4>(argument3.Name, argument3.Value, argument4.Name, argument4.Value));
            var lastFour = Combine(
                new TwoArguments<T5, T6>(argument5.Name, argument5.Value, argument6.Name, argument6.Value),
                new TwoArguments<T7, T8>(argument7.Name, argument7.Value, argument8.Name, argument8.Value));
            return FormatStoredCore(storage, message, culture, Combine(firstFour, lastFour));
        }

        internal static I18nMessageFormatResult FormatStored(
            I18nCompiledCatalogStorage storage,
            int message,
            CultureInfo culture,
            (string Name, object? Value)[] arguments)
        {
            return FormatStoredCore(storage, message, culture, new ManyArguments(arguments));
        }

        internal static I18nMessageFormatResult FormatStored<TArguments>(
            I18nCompiledCatalogStorage storage,
            int message,
            CultureInfo culture,
            TArguments arguments)
            where TArguments : struct, IArgumentSource
        {
            return FormatStoredCore(storage, message, culture, arguments);
        }

        private static I18nMessageFormatResult FormatStoredCore<TArguments>(
            I18nCompiledCatalogStorage storage,
            int messageIndex,
            CultureInfo culture,
            TArguments arguments)
            where TArguments : struct, IArgumentSource
        {
            return FormatProgram(
                new StoredProgramView(storage, messageIndex),
                culture,
                arguments);
        }

        private static I18nMessageFormatResult FormatProgram<TProgram, TArguments>(
            TProgram program,
            CultureInfo culture,
            TArguments arguments)
            where TProgram : struct, IMessageProgramView
            where TArguments : struct, IArgumentSource
        {
            int variant = I18nCompiledCatalogFormat.MissingIndex;
            if (program.SelectorCount > 0 &&
                !TrySelectVariant(
                    program,
                    culture,
                    arguments,
                    out variant,
                    out I18nMessageDiagnostic selectorDiagnostic))
            {
                return DiagnosticFallbackResult(selectorDiagnostic);
            }

            int partCount = variant == I18nCompiledCatalogFormat.MissingIndex
                ? program.PartCount
                : program.GetVariantPartCount(variant);
            if (partCount == 1)
            {
                ProgramPart onlyPart = variant == I18nCompiledCatalogFormat.MissingIndex
                    ? program.GetPart(0)
                    : program.GetVariantPart(variant, 0);
                if (onlyPart.Kind == MessagePartKind.Text)
                {
                    return new I18nMessageFormatResult(onlyPart.Value, NoDiagnostics);
                }
            }

            StringBuilder output = StringBuilderPool.Acquire();
            try
            {
                List<I18nMessageDiagnostic>? diagnostics = null;
                for (int index = 0; index < partCount; index++)
                {
                    ProgramPart part = variant == I18nCompiledCatalogFormat.MissingIndex
                        ? program.GetPart(index)
                        : program.GetVariantPart(variant, index);
                    if (part.Kind == MessagePartKind.Text)
                    {
                        output.Append(part.Value);
                        continue;
                    }

                    bool appended;
                    if (part.Kind == MessagePartKind.NumberVariable)
                    {
                        appended = arguments.TryGetNumber(part.Value, out decimal number);
                        if (appended)
                        {
                            if (part.NumberOptions.TryAppend(output, number, culture))
                            {
                                continue;
                            }

                            output.Append("{$").Append(part.Value).Append('}');
                            diagnostics ??= new List<I18nMessageDiagnostic>();
                            diagnostics.Add(UnsupportedNumberDiagnostic(part.Value, part.SourcePosition));
                            continue;
                        }
                    }
                    else
                    {
                        appended = arguments.TryAppend(part.Value, output);
                    }

                    if (appended)
                    {
                        continue;
                    }

                    output.Append("{$").Append(part.Value).Append('}');
                    diagnostics ??= new List<I18nMessageDiagnostic>();
                    diagnostics.Add(new I18nMessageDiagnostic(
                        I18nMessageDiagnosticCodes.MissingArgument,
                        $"Argument '{part.Value}' was not supplied.",
                        part.SourcePosition,
                        part.Value));
                }

                return new I18nMessageFormatResult(
                    output.ToString(),
                    diagnostics == null ? NoDiagnostics : diagnostics.AsReadOnly());
            }
            finally
            {
                StringBuilderPool.Release(output);
            }
        }

        private static bool TrySelectVariant<TProgram, TArguments>(
            TProgram program,
            CultureInfo culture,
            TArguments arguments,
            out int selectedVariant,
            out I18nMessageDiagnostic diagnostic)
            where TProgram : struct, IMessageProgramView
            where TArguments : struct, IArgumentSource
        {
            for (int index = 0; index < program.SelectorCount; index++)
            {
                ProgramSelector selector = program.GetSelector(index);
                bool found = selector.Kind == MessageSelectorKind.String
                    ? arguments.TryGetString(selector.Name, out _)
                    : arguments.TryGetNumber(selector.Name, out _);
                if (!found)
                {
                    selectedVariant = I18nCompiledCatalogFormat.MissingIndex;
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
                    selectedVariant = I18nCompiledCatalogFormat.MissingIndex;
                    diagnostic = UnsupportedNumberDiagnostic(selector.Name, -1);
                    return false;
                }
            }

            selectedVariant = I18nCompiledCatalogFormat.MissingIndex;
            for (int variant = 0; variant < program.VariantCount; variant++)
            {
                if (!VariantMatches(program, variant, culture, arguments))
                {
                    continue;
                }

                if (selectedVariant == I18nCompiledCatalogFormat.MissingIndex ||
                    IsVariantMoreSpecific(
                        program, variant, selectedVariant, culture, arguments))
                {
                    selectedVariant = variant;
                }
            }

            if (selectedVariant == I18nCompiledCatalogFormat.MissingIndex)
            {
                throw new InvalidOperationException("A compiled matcher must have a matching wildcard variant.");
            }

            diagnostic = default;
            return true;
        }

        private static bool VariantMatches<TProgram, TArguments>(
            TProgram program,
            int variant,
            CultureInfo culture,
            TArguments arguments)
            where TProgram : struct, IMessageProgramView
            where TArguments : struct, IArgumentSource
        {
            for (int selector = 0; selector < program.SelectorCount; selector++)
            {
                if (GetRank(program.GetSelector(selector), program.GetVariantKey(variant, selector), culture, arguments) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsVariantMoreSpecific<TProgram, TArguments>(
            TProgram program,
            int candidate,
            int current,
            CultureInfo culture,
            TArguments arguments)
            where TProgram : struct, IMessageProgramView
            where TArguments : struct, IArgumentSource
        {
            for (int selector = 0; selector < program.SelectorCount; selector++)
            {
                ProgramSelector selectorValue = program.GetSelector(selector);
                int candidateRank = GetRank(
                    selectorValue, program.GetVariantKey(candidate, selector), culture, arguments);
                int currentRank = GetRank(
                    selectorValue, program.GetVariantKey(current, selector), culture, arguments);
                if (candidateRank != currentRank)
                {
                    return candidateRank < currentRank;
                }
            }

            return false;
        }

        private static int GetRank<TArguments>(
            ProgramSelector selector,
            ProgramVariantKey key,
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

        private interface IMessageProgramView
        {
            int PartCount { get; }
            int SelectorCount { get; }
            int VariantCount { get; }
            ProgramPart GetPart(int index);
            ProgramSelector GetSelector(int index);
            int GetVariantPartCount(int variant);
            ProgramPart GetVariantPart(int variant, int index);
            ProgramVariantKey GetVariantKey(int variant, int selector);
        }

        private readonly struct ObjectProgramView : IMessageProgramView
        {
            private readonly I18nCompiledMessage _message;

            internal ObjectProgramView(I18nCompiledMessage message)
            {
                _message = message;
            }

            public int PartCount => _message._parts.Length;
            public int SelectorCount => _message._matcher?.Selectors.Length ?? 0;
            public int VariantCount => _message._matcher?.Variants.Length ?? 0;
            public ProgramPart GetPart(int index) => new ProgramPart(_message._parts[index]);
            public ProgramSelector GetSelector(int index) =>
                new ProgramSelector(_message._matcher!.Selectors[index]);
            public int GetVariantPartCount(int variant) => _message._matcher!.Variants[variant].Parts.Length;
            public ProgramPart GetVariantPart(int variant, int index) =>
                new ProgramPart(_message._matcher!.Variants[variant].Parts[index]);
            public ProgramVariantKey GetVariantKey(int variant, int selector) =>
                new ProgramVariantKey(_message._matcher!.Variants[variant].Keys[selector]);
        }

        private readonly struct StoredProgramView : IMessageProgramView
        {
            private readonly I18nCompiledCatalogStorage _storage;
            private readonly I18nCompiledMessageRecord _message;

            internal StoredProgramView(I18nCompiledCatalogStorage storage, int message)
            {
                _storage = storage;
                _message = storage.Messages[message];
            }

            public int PartCount => _message.PartCount;
            public int SelectorCount => _message.SelectorCount;
            public int VariantCount => _message.VariantCount;
            public ProgramPart GetPart(int index) =>
                new ProgramPart(_storage, _storage.MessageParts[_message.FirstPart + index]);
            public ProgramSelector GetSelector(int index) =>
                new ProgramSelector(_storage, _storage.Selectors[_message.FirstSelector + index]);
            public int GetVariantPartCount(int variant) =>
                _storage.Variants[_message.FirstVariant + variant].PartCount;
            public ProgramPart GetVariantPart(int variant, int index)
            {
                I18nCompiledVariantRecord record = _storage.Variants[_message.FirstVariant + variant];
                return new ProgramPart(_storage, _storage.MessageParts[record.FirstPart + index]);
            }
            public ProgramVariantKey GetVariantKey(int variant, int selector)
            {
                I18nCompiledVariantRecord record = _storage.Variants[_message.FirstVariant + variant];
                return new ProgramVariantKey(_storage, _storage.VariantKeys[record.FirstKey + selector]);
            }
        }

        private readonly struct ProgramPart
        {
            internal ProgramPart(MessagePart part)
            {
                Kind = part.Kind;
                Value = part.Value;
                SourcePosition = part.SourcePosition;
                NumberOptions = part.NumberOptions;
            }

            internal ProgramPart(I18nCompiledCatalogStorage storage, I18nCompiledMessagePartRecord part)
            {
                Kind = part.Kind;
                Value = storage.GetString(part.Value);
                SourcePosition = part.SourcePosition;
                NumberOptions = part.NumberOptions == I18nCompiledCatalogFormat.MissingIndex
                    ? default
                    : storage.NumberOptions[part.NumberOptions];
            }

            internal MessagePartKind Kind { get; }
            internal string Value { get; }
            internal int SourcePosition { get; }
            internal NumberOptions NumberOptions { get; }
        }

        private readonly struct ProgramSelector
        {
            internal ProgramSelector(MessageSelector selector)
            {
                Name = selector.Name;
                Kind = selector.Kind;
                NumberOptions = selector.NumberOptions;
            }

            internal ProgramSelector(I18nCompiledCatalogStorage storage, I18nCompiledSelectorRecord selector)
            {
                Name = storage.GetString(selector.Name);
                Kind = selector.Kind;
                NumberOptions = selector.NumberOptions == I18nCompiledCatalogFormat.MissingIndex
                    ? default
                    : storage.NumberOptions[selector.NumberOptions];
            }

            internal string Name { get; }
            internal MessageSelectorKind Kind { get; }
            internal NumberOptions NumberOptions { get; }
        }

        private readonly struct ProgramVariantKey
        {
            internal ProgramVariantKey(MessageVariantKey key)
            {
                Kind = key.Kind;
                Value = key.Value;
                ExactNumber = key.ExactNumber;
            }

            internal ProgramVariantKey(I18nCompiledCatalogStorage storage, I18nCompiledVariantKeyRecord key)
            {
                Kind = key.Kind;
                Value = storage.GetString(key.Value);
                ExactNumber = key.ExactNumber;
            }

            internal MessageVariantKeyKind Kind { get; }
            internal string Value { get; }
            internal decimal ExactNumber { get; }
        }

        private static class StringBuilderPool
        {
            private const int MaximumRetainedCapacity = 4096;

            [ThreadStatic]
            private static StringBuilder? _cached;

            internal static StringBuilder Acquire()
            {
                StringBuilder? builder = _cached;
                if (builder == null)
                {
                    return new StringBuilder();
                }

                _cached = null;
                return builder;
            }

            internal static void Release(StringBuilder builder)
            {
                if (builder.Capacity > MaximumRetainedCapacity)
                {
                    return;
                }

                builder.Clear();
                _cached = builder;
            }
        }
    }
}
