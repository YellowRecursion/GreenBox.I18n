using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace GreenBox.I18n
{
    /// <summary>
    /// Represents a message that has already been parsed and can be formatted repeatedly.
    /// </summary>
    public sealed partial class I18nCompiledMessage
    {
        private static readonly IReadOnlyList<I18nMessageDiagnostic> NoDiagnostics =
            Array.Empty<I18nMessageDiagnostic>();

        private readonly MessagePart[] _parts;
        private readonly MessageMatcher? _matcher;
        private readonly ReadOnlyCollection<string> _argumentNames;
        private readonly ReadOnlyCollection<I18nMessageArgumentKind> _argumentKinds;

        internal I18nCompiledMessage(
            MessagePart[] parts,
            MessageMatcher? matcher,
            List<string> argumentNames,
            List<I18nMessageArgumentKind> argumentKinds)
        {
            _parts = parts;
            _matcher = matcher;
            _argumentNames = argumentNames.AsReadOnly();
            _argumentKinds = argumentKinds.AsReadOnly();
        }

        /// <summary>Gets distinct argument names in their first source occurrence order.</summary>
        public IReadOnlyList<string> ArgumentNames => _argumentNames;

        internal MessagePart[] Parts => _parts;
        internal MessageMatcher? Matcher => _matcher;
        internal IReadOnlyList<I18nMessageArgumentKind> ArgumentKinds => _argumentKinds;

        /// <summary>Formats a message that has no external arguments.</summary>
        public I18nMessageFormatResult Format()
        {
            return FormatCore(CultureInfo.InvariantCulture, new NoArguments());
        }

        /// <summary>Formats a message with one named argument.</summary>
        public I18nMessageFormatResult Format<T1>((string Name, T1 Value) argument1)
        {
            return FormatCore(
                CultureInfo.InvariantCulture,
                new OneArgument<T1>(argument1.Name, argument1.Value));
        }

        /// <summary>Formats a message with two named arguments.</summary>
        public I18nMessageFormatResult Format<T1, T2>(
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2)
        {
            return FormatCore(
                CultureInfo.InvariantCulture,
                new TwoArguments<T1, T2>(
                    argument1.Name,
                    argument1.Value,
                    argument2.Name,
                    argument2.Value));
        }

        /// <summary>Formats a locale-aware message with one named argument.</summary>
        public I18nMessageFormatResult Format<T1>(
            CultureInfo culture,
            (string Name, T1 Value) argument1)
        {
            if (culture == null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            return FormatCore(culture, new OneArgument<T1>(argument1.Name, argument1.Value));
        }

        /// <summary>Formats a locale-aware message with two named arguments.</summary>
        public I18nMessageFormatResult Format<T1, T2>(
            CultureInfo culture,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2)
        {
            if (culture == null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            return FormatCore(
                culture,
                new TwoArguments<T1, T2>(
                    argument1.Name,
                    argument1.Value,
                    argument2.Name,
                    argument2.Value));
        }

        /// <summary>Formats a message with three named arguments without allocating an argument array.</summary>
        public I18nMessageFormatResult Format<T1, T2, T3>(
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3)
        {
            return Format(CultureInfo.InvariantCulture, argument1, argument2, argument3);
        }

        /// <summary>Formats a locale-aware message with three named arguments.</summary>
        public I18nMessageFormatResult Format<T1, T2, T3>(
            CultureInfo culture,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3)
        {
            EnsureCulture(culture);
            return FormatCore(
                culture,
                Combine(
                    new TwoArguments<T1, T2>(argument1.Name, argument1.Value, argument2.Name, argument2.Value),
                    new OneArgument<T3>(argument3.Name, argument3.Value)));
        }

        /// <summary>Formats a message with four named arguments without allocating an argument array.</summary>
        public I18nMessageFormatResult Format<T1, T2, T3, T4>(
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4)
        {
            return Format(CultureInfo.InvariantCulture, argument1, argument2, argument3, argument4);
        }

        /// <summary>Formats a locale-aware message with four named arguments.</summary>
        public I18nMessageFormatResult Format<T1, T2, T3, T4>(
            CultureInfo culture,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4)
        {
            EnsureCulture(culture);
            return FormatCore(
                culture,
                Combine(
                    new TwoArguments<T1, T2>(argument1.Name, argument1.Value, argument2.Name, argument2.Value),
                    new TwoArguments<T3, T4>(argument3.Name, argument3.Value, argument4.Name, argument4.Value)));
        }

        /// <summary>Formats a message with five named arguments without allocating an argument array.</summary>
        public I18nMessageFormatResult Format<T1, T2, T3, T4, T5>(
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5)
        {
            return Format(CultureInfo.InvariantCulture, argument1, argument2, argument3, argument4, argument5);
        }

        /// <summary>Formats a locale-aware message with five named arguments.</summary>
        public I18nMessageFormatResult Format<T1, T2, T3, T4, T5>(
            CultureInfo culture,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5)
        {
            EnsureCulture(culture);
            var firstFour = Combine(
                new TwoArguments<T1, T2>(argument1.Name, argument1.Value, argument2.Name, argument2.Value),
                new TwoArguments<T3, T4>(argument3.Name, argument3.Value, argument4.Name, argument4.Value));
            return FormatCore(culture, Combine(firstFour, new OneArgument<T5>(argument5.Name, argument5.Value)));
        }

        /// <summary>Formats a message with six named arguments without allocating an argument array.</summary>
        public I18nMessageFormatResult Format<T1, T2, T3, T4, T5, T6>(
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5,
            (string Name, T6 Value) argument6)
        {
            return Format(CultureInfo.InvariantCulture, argument1, argument2, argument3, argument4, argument5, argument6);
        }

        /// <summary>Formats a locale-aware message with six named arguments.</summary>
        public I18nMessageFormatResult Format<T1, T2, T3, T4, T5, T6>(
            CultureInfo culture,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5,
            (string Name, T6 Value) argument6)
        {
            EnsureCulture(culture);
            var firstFour = Combine(
                new TwoArguments<T1, T2>(argument1.Name, argument1.Value, argument2.Name, argument2.Value),
                new TwoArguments<T3, T4>(argument3.Name, argument3.Value, argument4.Name, argument4.Value));
            return FormatCore(
                culture,
                Combine(firstFour, new TwoArguments<T5, T6>(argument5.Name, argument5.Value, argument6.Name, argument6.Value)));
        }

        /// <summary>Formats a message with seven named arguments without allocating an argument array.</summary>
        public I18nMessageFormatResult Format<T1, T2, T3, T4, T5, T6, T7>(
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5,
            (string Name, T6 Value) argument6,
            (string Name, T7 Value) argument7)
        {
            return Format(CultureInfo.InvariantCulture, argument1, argument2, argument3, argument4, argument5, argument6, argument7);
        }

        /// <summary>Formats a locale-aware message with seven named arguments.</summary>
        public I18nMessageFormatResult Format<T1, T2, T3, T4, T5, T6, T7>(
            CultureInfo culture,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5,
            (string Name, T6 Value) argument6,
            (string Name, T7 Value) argument7)
        {
            EnsureCulture(culture);
            var firstFour = Combine(
                new TwoArguments<T1, T2>(argument1.Name, argument1.Value, argument2.Name, argument2.Value),
                new TwoArguments<T3, T4>(argument3.Name, argument3.Value, argument4.Name, argument4.Value));
            var lastThree = Combine(
                new TwoArguments<T5, T6>(argument5.Name, argument5.Value, argument6.Name, argument6.Value),
                new OneArgument<T7>(argument7.Name, argument7.Value));
            return FormatCore(culture, Combine(firstFour, lastThree));
        }

        /// <summary>Formats a message with eight named arguments without allocating an argument array.</summary>
        public I18nMessageFormatResult Format<T1, T2, T3, T4, T5, T6, T7, T8>(
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5,
            (string Name, T6 Value) argument6,
            (string Name, T7 Value) argument7,
            (string Name, T8 Value) argument8)
        {
            return Format(CultureInfo.InvariantCulture, argument1, argument2, argument3, argument4, argument5, argument6, argument7, argument8);
        }

        /// <summary>Formats a locale-aware message with eight named arguments.</summary>
        public I18nMessageFormatResult Format<T1, T2, T3, T4, T5, T6, T7, T8>(
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
            EnsureCulture(culture);
            var firstFour = Combine(
                new TwoArguments<T1, T2>(argument1.Name, argument1.Value, argument2.Name, argument2.Value),
                new TwoArguments<T3, T4>(argument3.Name, argument3.Value, argument4.Name, argument4.Value));
            var lastFour = Combine(
                new TwoArguments<T5, T6>(argument5.Name, argument5.Value, argument6.Name, argument6.Value),
                new TwoArguments<T7, T8>(argument7.Name, argument7.Value, argument8.Name, argument8.Value));
            return FormatCore(culture, Combine(firstFour, lastFour));
        }

        /// <summary>Formats a message with an uncommon number of named arguments.</summary>
        public I18nMessageFormatResult Format(params (string Name, object? Value)[] arguments)
        {
            return Format(CultureInfo.InvariantCulture, arguments);
        }

        /// <summary>Formats a locale-aware message with an uncommon number of named arguments.</summary>
        public I18nMessageFormatResult Format(
            CultureInfo culture,
            params (string Name, object? Value)[] arguments)
        {
            EnsureCulture(culture);
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            return FormatCore(culture, new ManyArguments(arguments));
        }

        private static void EnsureCulture(CultureInfo culture)
        {
            if (culture == null)
            {
                throw new ArgumentNullException(nameof(culture));
            }
        }

        private I18nMessageFormatResult FormatCore<TArguments>(CultureInfo culture, TArguments arguments)
            where TArguments : struct, IArgumentSource
        {
            return FormatProgram(new ObjectProgramView(this), culture, arguments);
        }

        private static I18nMessageFormatResult DiagnosticFallbackResult(I18nMessageDiagnostic diagnostic)
        {
            return new I18nMessageFormatResult(
                "{$" + diagnostic.ArgumentName + "}",
                new[] { diagnostic });
        }

        private static I18nMessageDiagnostic UnsupportedNumberDiagnostic(string argumentName, int position)
        {
            return new I18nMessageDiagnostic(
                I18nMessageDiagnosticCodes.UnsupportedOperation,
                $"Numeric argument '{argumentName}' cannot be formatted because the transformed value exceeds the supported range.",
                position,
                argumentName);
        }
    }
}
