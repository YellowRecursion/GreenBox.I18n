using System;
using System.Globalization;
using System.Text;

namespace GreenBox.I18n
{
    public sealed partial class I18nCompiledMessage
    {
        internal readonly struct NumberOptions : IEquatable<NumberOptions>
        {
            private readonly string? _ungroupedPattern;
            private readonly string? _groupedPattern;
            private readonly string? _integerPattern;
            private readonly string? _groupedIntegerPattern;

            public NumberOptions(
                int minimumFractionDigits,
                int maximumFractionDigits,
                int minimumSignificantDigits,
                int maximumSignificantDigits,
                int minimumIntegerDigits,
                int roundingIncrement,
                NumberGrouping grouping,
                NumberSignDisplay signDisplay,
                TrailingZeroDisplay trailingZeroDisplay,
                NumberRoundingMode roundingMode,
                NumberRoundingPriority roundingPriority,
                NumberStyle style,
                decimal offset)
            {
                MinimumFractionDigits = minimumFractionDigits;
                MaximumFractionDigits = maximumFractionDigits;
                MinimumSignificantDigits = minimumSignificantDigits;
                MaximumSignificantDigits = maximumSignificantDigits;
                MinimumIntegerDigits = minimumIntegerDigits;
                RoundingIncrement = roundingIncrement;
                Grouping = grouping;
                SignDisplay = signDisplay;
                TrailingZeroDisplay = trailingZeroDisplay;
                RoundingMode = roundingMode;
                RoundingPriority = roundingPriority;
                Style = style;
                Offset = offset;
                _integerPattern = new string('0', minimumIntegerDigits);
                _groupedIntegerPattern = "#,##" + _integerPattern;
                if (maximumSignificantDigits < 0 &&
                    trailingZeroDisplay != TrailingZeroDisplay.StripIfInteger &&
                    maximumFractionDigits >= 0)
                {
                    _ungroupedPattern = BuildPattern(
                        _integerPattern,
                        minimumFractionDigits,
                        maximumFractionDigits);
                    _groupedPattern = BuildPattern(
                        _groupedIntegerPattern,
                        minimumFractionDigits,
                        maximumFractionDigits);
                }
                else
                {
                    _ungroupedPattern = null;
                    _groupedPattern = null;
                }
            }

            public static NumberOptions Default => new NumberOptions(
                -1,
                -1,
                -1,
                -1,
                1,
                1,
                NumberGrouping.Auto,
                NumberSignDisplay.Auto,
                TrailingZeroDisplay.Auto,
                NumberRoundingMode.HalfExpand,
                NumberRoundingPriority.Auto,
                NumberStyle.Decimal,
                0m);

            public int MinimumFractionDigits { get; }
            public int MaximumFractionDigits { get; }
            public int MinimumSignificantDigits { get; }
            public int MaximumSignificantDigits { get; }
            public int MinimumIntegerDigits { get; }
            public int RoundingIncrement { get; }
            public NumberGrouping Grouping { get; }
            public NumberSignDisplay SignDisplay { get; }
            public TrailingZeroDisplay TrailingZeroDisplay { get; }
            public NumberRoundingMode RoundingMode { get; }
            public NumberRoundingPriority RoundingPriority { get; }
            public NumberStyle Style { get; }
            public decimal Offset { get; }

            internal bool IsValid => AreValid(
                MinimumFractionDigits,
                MaximumFractionDigits,
                MinimumSignificantDigits,
                MaximumSignificantDigits,
                MinimumIntegerDigits,
                RoundingIncrement,
                Grouping,
                SignDisplay,
                TrailingZeroDisplay,
                RoundingMode,
                RoundingPriority,
                Style);

            internal static bool AreValid(
                int minimumFractionDigits,
                int maximumFractionDigits,
                int minimumSignificantDigits,
                int maximumSignificantDigits,
                int minimumIntegerDigits,
                int roundingIncrement,
                NumberGrouping grouping,
                NumberSignDisplay signDisplay,
                TrailingZeroDisplay trailingZeroDisplay,
                NumberRoundingMode roundingMode,
                NumberRoundingPriority roundingPriority,
                NumberStyle style)
            {
                return IsOptionalDigitSize(minimumFractionDigits, false) &&
                       IsOptionalDigitSize(maximumFractionDigits, false) &&
                       IsOptionalDigitSize(minimumSignificantDigits, true) &&
                       IsOptionalDigitSize(maximumSignificantDigits, true) &&
                       minimumIntegerDigits >= 1 && minimumIntegerDigits <= 28 &&
                       IsSupportedRoundingIncrement(roundingIncrement) &&
                       (minimumFractionDigits < 0 || maximumFractionDigits < 0 ||
                        minimumFractionDigits <= maximumFractionDigits) &&
                       (minimumSignificantDigits < 0 ||
                        maximumSignificantDigits >= minimumSignificantDigits) &&
                       (roundingIncrement == 1 ||
                        (minimumFractionDigits >= 0 &&
                         minimumFractionDigits == maximumFractionDigits &&
                         maximumSignificantDigits < 0)) &&
                       grouping >= NumberGrouping.Auto && grouping <= NumberGrouping.Min2 &&
                       signDisplay >= NumberSignDisplay.Auto &&
                       signDisplay <= NumberSignDisplay.Never &&
                       trailingZeroDisplay >= TrailingZeroDisplay.Auto &&
                       trailingZeroDisplay <= TrailingZeroDisplay.StripIfInteger &&
                       roundingMode >= NumberRoundingMode.Ceil &&
                       roundingMode <= NumberRoundingMode.HalfEven &&
                       roundingPriority >= NumberRoundingPriority.Auto &&
                       roundingPriority <= NumberRoundingPriority.LessPrecision &&
                       style >= NumberStyle.Decimal && style <= NumberStyle.Percent;
            }

            public bool Equals(NumberOptions other)
            {
                return MinimumFractionDigits == other.MinimumFractionDigits &&
                       MaximumFractionDigits == other.MaximumFractionDigits &&
                       MinimumSignificantDigits == other.MinimumSignificantDigits &&
                       MaximumSignificantDigits == other.MaximumSignificantDigits &&
                       MinimumIntegerDigits == other.MinimumIntegerDigits &&
                       RoundingIncrement == other.RoundingIncrement &&
                       Grouping == other.Grouping &&
                       SignDisplay == other.SignDisplay &&
                       TrailingZeroDisplay == other.TrailingZeroDisplay &&
                       RoundingMode == other.RoundingMode &&
                       RoundingPriority == other.RoundingPriority &&
                       Style == other.Style &&
                       Offset == other.Offset;
            }

            public override bool Equals(object? obj)
            {
                return obj is NumberOptions other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = MinimumFractionDigits;
                    hash = (hash * 397) ^ MaximumFractionDigits;
                    hash = (hash * 397) ^ MinimumSignificantDigits;
                    hash = (hash * 397) ^ MaximumSignificantDigits;
                    hash = (hash * 397) ^ MinimumIntegerDigits;
                    hash = (hash * 397) ^ RoundingIncrement;
                    hash = (hash * 397) ^ (int)Grouping;
                    hash = (hash * 397) ^ (int)SignDisplay;
                    hash = (hash * 397) ^ (int)TrailingZeroDisplay;
                    hash = (hash * 397) ^ (int)RoundingMode;
                    hash = (hash * 397) ^ (int)RoundingPriority;
                    hash = (hash * 397) ^ (int)Style;
                    return (hash * 397) ^ Offset.GetHashCode();
                }
            }

            public NumberOptions WithOffset(decimal offset)
            {
                return new NumberOptions(
                    MinimumFractionDigits,
                    MaximumFractionDigits,
                    MinimumSignificantDigits,
                    MaximumSignificantDigits,
                    MinimumIntegerDigits,
                    RoundingIncrement,
                    Grouping,
                    SignDisplay,
                    TrailingZeroDisplay,
                    RoundingMode,
                    RoundingPriority,
                    Style,
                    Offset + offset);
            }

            public bool TryPrepare(decimal value, out decimal result)
            {
                try
                {
                    result = Prepare(value, out _, out _);
                    return true;
                }
                catch (Exception exception) when (
                    exception is OverflowException ||
                    exception is ArgumentOutOfRangeException ||
                    exception is InvalidOperationException)
                {
                    result = default;
                    return false;
                }
            }

            private decimal Prepare(decimal value, out bool useSignificant, out int preparedScale)
            {
                value += Offset;
                if (Style == NumberStyle.Percent)
                {
                    value *= 100m;
                }

                decimal fractionResult = PrepareFraction(value);
                decimal significantResult = PrepareSignificant(value);
                bool hasFraction = MaximumFractionDigits >= 0;
                bool hasSignificant = MaximumSignificantDigits >= 0;
                decimal result;
                if (hasSignificant && (!hasFraction || RoundingPriority == NumberRoundingPriority.Auto))
                {
                    result = significantResult;
                    useSignificant = true;
                }
                else if (hasFraction && hasSignificant)
                {
                    int fractionMagnitude = -MaximumFractionDigits;
                    int significantMagnitude =
                        IntegerDigitCount(Math.Abs(significantResult)) - MaximumSignificantDigits;
                    bool fractionIsMorePrecise = fractionMagnitude < significantMagnitude;
                    useSignificant = RoundingPriority == NumberRoundingPriority.MorePrecision
                        ? !fractionIsMorePrecise
                        : fractionIsMorePrecise;
                    result = useSignificant ? significantResult : fractionResult;
                }
                else
                {
                    result = fractionResult;
                    useSignificant = false;
                }
                int[] bits = decimal.GetBits(result);
                int currentScale = (bits[3] >> 16) & 0x7F;
                int requiredScale = currentScale;
                if (useSignificant && MinimumSignificantDigits >= 0)
                {
                    requiredScale = Math.Max(
                        requiredScale,
                        Math.Max(0, MinimumSignificantDigits - IntegerDigitCount(Math.Abs(result))));
                }
                else if (!useSignificant && MinimumFractionDigits >= 0)
                {
                    requiredScale = Math.Max(requiredScale, MinimumFractionDigits);
                }

                if (!useSignificant && MaximumFractionDigits >= 0)
                {
                    requiredScale = Math.Min(requiredScale, MaximumFractionDigits);
                }

                preparedScale = Math.Min(requiredScale, 28);
                decimal prepared = WithScale(bits, currentScale, preparedScale);
                if (TrailingZeroDisplay == TrailingZeroDisplay.StripIfInteger &&
                    prepared == decimal.Truncate(prepared))
                {
                    preparedScale = 0;
                    return decimal.Truncate(prepared);
                }

                return prepared;
            }

            private static decimal WithScale(int[] bits, int scale, int targetScale)
            {
                uint low = unchecked((uint)bits[0]);
                uint middle = unchecked((uint)bits[1]);
                uint high = unchecked((uint)bits[2]);
                bool negative = (bits[3] & int.MinValue) != 0;

                while (scale < targetScale)
                {
                    ulong product = (ulong)low * 10UL;
                    low = (uint)product;
                    ulong carry = product >> 32;

                    product = (ulong)middle * 10UL + carry;
                    middle = (uint)product;
                    carry = product >> 32;

                    product = (ulong)high * 10UL + carry;
                    high = (uint)product;
                    if ((product >> 32) != 0)
                    {
                        throw new OverflowException();
                    }

                    scale++;
                }

                while (scale > targetScale)
                {
                    ulong remainder = high % 10UL;
                    high /= 10U;

                    ulong dividend = (remainder << 32) | middle;
                    middle = (uint)(dividend / 10UL);
                    remainder = dividend % 10UL;

                    dividend = (remainder << 32) | low;
                    low = (uint)(dividend / 10UL);
                    if (dividend % 10UL != 0)
                    {
                        throw new InvalidOperationException(
                            "A rounded decimal cannot be represented at its required scale.");
                    }

                    scale--;
                }

                return new decimal(
                    unchecked((int)low),
                    unchecked((int)middle),
                    unchecked((int)high),
                    negative,
                    (byte)targetScale);
            }

            private decimal PrepareFraction(decimal value)
            {
                if (MaximumFractionDigits < 0)
                {
                    return value;
                }

                if (RoundingIncrement == 1)
                {
                    return Round(value, MaximumFractionDigits, RoundingMode);
                }

                decimal increment = RoundingIncrement / Pow10(MaximumFractionDigits);
                return Round(value / increment, 0, RoundingMode) * increment;
            }

            private decimal PrepareSignificant(decimal value)
            {
                if (MaximumSignificantDigits < 0 || value == 0m)
                {
                    return value;
                }

                int integerDigits = IntegerDigitCount(Math.Abs(value));
                int fractionDigits = MaximumSignificantDigits - integerDigits;
                return Round(value, fractionDigits, RoundingMode);
            }

            public bool TryAppend(StringBuilder output, decimal value, CultureInfo culture)
            {
                try
                {
                    Append(output, value, culture);
                    return true;
                }
                catch (Exception exception) when (
                    exception is OverflowException ||
                    exception is ArgumentOutOfRangeException ||
                    exception is InvalidOperationException)
                {
                    return false;
                }
            }

            private void Append(StringBuilder output, decimal value, CultureInfo culture)
            {
                decimal transformedSource = value + Offset;
                if (Style == NumberStyle.Percent)
                {
                    transformedSource *= 100m;
                }

                bool sourceIsNegative = IsNegative(transformedSource);
                decimal prepared = Prepare(value, out bool useSignificant, out int scale);
                decimal absolute = Math.Abs(prepared);
                int minimum = MinimumFractionDigits >= 0 ? MinimumFractionDigits : scale;
                int maximum = MaximumFractionDigits >= 0 ? MaximumFractionDigits : Math.Max(scale, minimum);
                if (MaximumSignificantDigits >= 0 &&
                    useSignificant)
                {
                    int integerDigits = IntegerDigitCount(absolute);
                    minimum = MinimumSignificantDigits >= 0
                        ? Math.Max(0, MinimumSignificantDigits - integerDigits)
                        : 0;
                    maximum = Math.Max(0, MaximumSignificantDigits - integerDigits);
                }
                if (TrailingZeroDisplay == TrailingZeroDisplay.StripIfInteger &&
                    prepared == decimal.Truncate(prepared))
                {
                    minimum = 0;
                    maximum = 0;
                }
                bool grouping = Grouping switch
                {
                    NumberGrouping.Never => false,
                    NumberGrouping.Min2 => absolute >= 10000m,
                    _ => true,
                };

                string pattern = grouping
                    ? _groupedPattern ?? BuildPattern(
                        _groupedIntegerPattern ?? "#,##0", minimum, maximum)
                    : _ungroupedPattern ?? BuildPattern(
                        _integerPattern ?? "0", minimum, maximum);

                bool showNegative = sourceIsNegative &&
                    SignDisplay != NumberSignDisplay.Never &&
                    (SignDisplay != NumberSignDisplay.Negative || prepared != 0m) &&
                    (SignDisplay != NumberSignDisplay.ExceptZero || prepared != 0m);
                bool showPositive = !sourceIsNegative &&
                    (SignDisplay == NumberSignDisplay.Always ||
                     (SignDisplay == NumberSignDisplay.ExceptZero && prepared != 0m));
                if (Style == NumberStyle.Percent)
                {
                    AppendPercent(
                        output,
                        absolute,
                        pattern,
                        culture,
                        showNegative,
                        showPositive);
                }
                else
                {
                    if (showNegative)
                    {
                        output.Append(culture.NumberFormat.NegativeSign);
                    }
                    else if (showPositive)
                    {
                        output.Append(culture.NumberFormat.PositiveSign);
                    }

                    AppendFormattedNumber(output, absolute, pattern, culture);
                }
            }

            private static string BuildPattern(
                string integerPattern,
                int minimumFractionDigits,
                int maximumFractionDigits)
            {
                if (maximumFractionDigits <= 0)
                {
                    return integerPattern;
                }

                return integerPattern + "." +
                    new string('0', Math.Max(0, minimumFractionDigits)) +
                    new string('#', maximumFractionDigits - Math.Max(0, minimumFractionDigits));
            }

            private static void AppendFormattedNumber(
                StringBuilder output,
                decimal value,
                string pattern,
                CultureInfo culture)
            {
                Span<char> buffer = stackalloc char[128];
                if (value.TryFormat(buffer, out int charsWritten, pattern, culture))
                {
                    output.Append(buffer.Slice(0, charsWritten));
                    return;
                }

                output.Append(value.ToString(pattern, culture));
            }

            private static void AppendPercent(
                StringBuilder output,
                decimal number,
                string pattern,
                CultureInfo culture,
                bool showNegative,
                bool showPositive)
            {
                NumberFormatInfo format = culture.NumberFormat;
                if (showNegative)
                {
                    AppendNegativePercent(output, number, pattern, culture);
                    return;
                }

                switch (format.PercentPositivePattern)
                {
                    case 0:
                        AppendPositiveNumber(output, number, pattern, culture, showPositive);
                        output.Append(' ').Append(format.PercentSymbol);
                        break;
                    case 2:
                        output.Append(format.PercentSymbol);
                        AppendPositiveNumber(output, number, pattern, culture, showPositive);
                        break;
                    case 3:
                        output.Append(format.PercentSymbol).Append(' ');
                        AppendPositiveNumber(output, number, pattern, culture, showPositive);
                        break;
                    default:
                        AppendPositiveNumber(output, number, pattern, culture, showPositive);
                        output.Append(format.PercentSymbol);
                        break;
                }
            }

            private static void AppendPositiveNumber(
                StringBuilder output,
                decimal number,
                string pattern,
                CultureInfo culture,
                bool showPositive)
            {
                if (showPositive)
                {
                    output.Append(culture.NumberFormat.PositiveSign);
                }

                AppendFormattedNumber(output, number, pattern, culture);
            }

            private static void AppendNegativePercent(
                StringBuilder output,
                decimal number,
                string pattern,
                CultureInfo culture)
            {
                NumberFormatInfo format = culture.NumberFormat;
                string sign = format.NegativeSign;
                string symbol = format.PercentSymbol;
                switch (format.PercentNegativePattern)
                {
                    case 0:
                        output.Append(sign);
                        AppendFormattedNumber(output, number, pattern, culture);
                        output.Append(' ').Append(symbol);
                        break;
                    case 1:
                        output.Append(sign);
                        AppendFormattedNumber(output, number, pattern, culture);
                        output.Append(symbol);
                        break;
                    case 2:
                        output.Append(sign).Append(symbol);
                        AppendFormattedNumber(output, number, pattern, culture);
                        break;
                    case 3:
                        output.Append(symbol).Append(sign);
                        AppendFormattedNumber(output, number, pattern, culture);
                        break;
                    case 4:
                        output.Append(symbol);
                        AppendFormattedNumber(output, number, pattern, culture);
                        output.Append(sign);
                        break;
                    case 5:
                        AppendFormattedNumber(output, number, pattern, culture);
                        output.Append(sign).Append(symbol);
                        break;
                    case 6:
                        AppendFormattedNumber(output, number, pattern, culture);
                        output.Append(symbol).Append(sign);
                        break;
                    case 7:
                        output.Append(sign).Append(symbol).Append(' ');
                        AppendFormattedNumber(output, number, pattern, culture);
                        break;
                    case 8:
                        AppendFormattedNumber(output, number, pattern, culture);
                        output.Append(' ').Append(symbol).Append(sign);
                        break;
                    case 9:
                        output.Append(symbol).Append(' ');
                        AppendFormattedNumber(output, number, pattern, culture);
                        output.Append(sign);
                        break;
                    case 10:
                        output.Append(symbol).Append(' ').Append(sign);
                        AppendFormattedNumber(output, number, pattern, culture);
                        break;
                    case 11:
                        AppendFormattedNumber(output, number, pattern, culture);
                        output.Append(sign).Append(' ').Append(symbol);
                        break;
                    default:
                        output.Append(sign);
                        AppendFormattedNumber(output, number, pattern, culture);
                        output.Append(symbol);
                        break;
                }
            }

            private static decimal Round(decimal value, int digits, NumberRoundingMode mode)
            {
                if (digits < 0)
                {
                    decimal unit = Pow10(-digits);
                    return Round(value / unit, 0, mode) * unit;
                }

                int currentScale = GetScale(value);
                if (currentScale <= digits)
                {
                    return value;
                }

                if (mode == NumberRoundingMode.HalfExpand)
                {
                    return Math.Round(value, digits, MidpointRounding.AwayFromZero);
                }

                if (mode == NumberRoundingMode.HalfEven)
                {
                    return Math.Round(value, digits, MidpointRounding.ToEven);
                }

                decimal factor = Pow10(digits);
                decimal scaled = value * factor;
                decimal rounded = mode switch
                {
                    NumberRoundingMode.Ceil => decimal.Ceiling(scaled),
                    NumberRoundingMode.Floor => decimal.Floor(scaled),
                    NumberRoundingMode.Expand => Math.Sign(scaled) * decimal.Ceiling(Math.Abs(scaled)),
                    NumberRoundingMode.Trunc => decimal.Truncate(scaled),
                    NumberRoundingMode.HalfCeil => RoundHalf(scaled, decimal.Ceiling(scaled)),
                    NumberRoundingMode.HalfFloor => RoundHalf(scaled, decimal.Floor(scaled)),
                    NumberRoundingMode.HalfTrunc => RoundHalf(scaled, decimal.Truncate(scaled)),
                    _ => throw new ArgumentOutOfRangeException(nameof(mode)),
                };
                return rounded / factor;
            }

            private static decimal RoundHalf(decimal value, decimal tieResult)
            {
                decimal floor = decimal.Floor(value);
                decimal distance = value - floor;
                if (distance < 0.5m)
                {
                    return floor;
                }

                if (distance > 0.5m)
                {
                    return decimal.Ceiling(value);
                }

                return tieResult;
            }

            private static decimal Pow10(int exponent)
            {
                decimal result = 1m;
                for (int i = 0; i < exponent; i++)
                {
                    result *= 10m;
                }

                return result;
            }

            private static bool IsOptionalDigitSize(int value, bool positive)
            {
                return value == -1 ||
                       (value >= (positive ? 1 : 0) && value <= 28);
            }

            private static bool IsSupportedRoundingIncrement(int value)
            {
                return value == 1 || value == 2 || value == 5 || value == 10 ||
                       value == 20 || value == 25 || value == 50 || value == 100 ||
                       value == 200 || value == 250 || value == 500 || value == 1000 ||
                       value == 2000 || value == 2500 || value == 5000;
            }

            private static int IntegerDigitCount(decimal value)
            {
                if (value >= 1m)
                {
                    int digits = 0;
                    for (decimal integer = decimal.Truncate(value); integer >= 1m; integer /= 10m)
                    {
                        digits++;
                    }

                    return digits;
                }

                int leadingFractionZeros = 0;
                decimal shifted = value;
                while (shifted > 0m && shifted < 1m)
                {
                    shifted *= 10m;
                    leadingFractionZeros++;
                }

                return 1 - leadingFractionZeros;
            }

            private static int GetScale(decimal value)
            {
                int[] bits = decimal.GetBits(value);
                return (bits[3] >> 16) & 0x7F;
            }

            private static bool IsNegative(decimal value)
            {
                int[] bits = decimal.GetBits(value);
                return (bits[3] & int.MinValue) != 0;
            }
        }

        internal enum NumberGrouping : byte
        {
            Auto,
            Always,
            Never,
            Min2,
        }

        internal enum NumberSignDisplay : byte
        {
            Auto,
            Always,
            ExceptZero,
            Negative,
            Never,
        }

        internal enum TrailingZeroDisplay : byte
        {
            Auto,
            StripIfInteger,
        }

        internal enum NumberRoundingMode : byte
        {
            Ceil,
            Floor,
            Expand,
            Trunc,
            HalfCeil,
            HalfFloor,
            HalfExpand,
            HalfTrunc,
            HalfEven,
        }

        internal enum NumberRoundingPriority : byte
        {
            Auto,
            MorePrecision,
            LessPrecision,
        }

        internal enum NumberStyle : byte
        {
            Decimal,
            Percent,
        }
    }
}
