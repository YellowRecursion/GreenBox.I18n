using System;
using System.Collections.Generic;
using System.Globalization;

namespace GreenBox.I18n
{
    /// <summary>
    /// Compares i18n entries using the canonical natural path order and numeric ID tie-breaker.
    /// </summary>
    /// <remarks>
    /// Text is compared ordinally without case, digit runs are compared by numeric magnitude,
    /// and shorter digit runs precede longer runs when their numeric values are equal.
    /// </remarks>
    public sealed class I18nEntryComparer : IComparer<I18nEntry>
    {
        /// <summary>
        /// Gets the shared canonical entry comparer.
        /// </summary>
        public static I18nEntryComparer Canonical { get; } = new I18nEntryComparer();

        private I18nEntryComparer()
        {
        }

        /// <inheritdoc />
        public int Compare(I18nEntry? x, I18nEntry? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x == null)
            {
                return -1;
            }

            if (y == null)
            {
                return 1;
            }

            int pathComparison = CompareNaturalIgnoreCase(x.Path, y.Path);
            if (pathComparison != 0)
            {
                return pathComparison;
            }

            pathComparison = StringComparer.Ordinal.Compare(x.Path, y.Path);
            if (pathComparison != 0)
            {
                return pathComparison;
            }

            return CompareIds(x.Id, y.Id);
        }

        private static int CompareNaturalIgnoreCase(string? x, string? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x == null)
            {
                return -1;
            }

            if (y == null)
            {
                return 1;
            }

            int xIndex = 0;
            int yIndex = 0;

            while (xIndex < x.Length && yIndex < y.Length)
            {
                bool xIsDigit = IsAsciiDigit(x[xIndex]);
                bool yIsDigit = IsAsciiDigit(y[yIndex]);

                if (xIsDigit && yIsDigit)
                {
                    int numberComparison = CompareNumberRuns(x, ref xIndex, y, ref yIndex);
                    if (numberComparison != 0)
                    {
                        return numberComparison;
                    }

                    continue;
                }

                char xCharacter = char.ToUpperInvariant(x[xIndex]);
                char yCharacter = char.ToUpperInvariant(y[yIndex]);
                int characterComparison = xCharacter.CompareTo(yCharacter);
                if (characterComparison != 0)
                {
                    return characterComparison;
                }

                xIndex++;
                yIndex++;
            }

            return (x.Length - xIndex).CompareTo(y.Length - yIndex);
        }

        private static int CompareNumberRuns(
            string x,
            ref int xIndex,
            string y,
            ref int yIndex)
        {
            int xStart = xIndex;
            int yStart = yIndex;

            while (xIndex < x.Length && IsAsciiDigit(x[xIndex]))
            {
                xIndex++;
            }

            while (yIndex < y.Length && IsAsciiDigit(y[yIndex]))
            {
                yIndex++;
            }

            int xSignificantStart = SkipLeadingZeroes(x, xStart, xIndex);
            int ySignificantStart = SkipLeadingZeroes(y, yStart, yIndex);
            int xSignificantLength = xIndex - xSignificantStart;
            int ySignificantLength = yIndex - ySignificantStart;

            int lengthComparison = xSignificantLength.CompareTo(ySignificantLength);
            if (lengthComparison != 0)
            {
                return lengthComparison;
            }

            for (int offset = 0; offset < xSignificantLength; offset++)
            {
                int digitComparison = x[xSignificantStart + offset].CompareTo(y[ySignificantStart + offset]);
                if (digitComparison != 0)
                {
                    return digitComparison;
                }
            }

            int xRunLength = xIndex - xStart;
            int yRunLength = yIndex - yStart;
            return xRunLength.CompareTo(yRunLength);
        }

        private static int SkipLeadingZeroes(string value, int start, int end)
        {
            int index = start;
            while (index < end && value[index] == '0')
            {
                index++;
            }

            return index;
        }

        private static bool IsAsciiDigit(char value)
        {
            return value >= '0' && value <= '9';
        }

        private static int CompareIds(string? x, string? y)
        {
            bool xIsNumeric = long.TryParse(x, NumberStyles.None, CultureInfo.InvariantCulture, out long xId);
            bool yIsNumeric = long.TryParse(y, NumberStyles.None, CultureInfo.InvariantCulture, out long yId);

            if (xIsNumeric && yIsNumeric)
            {
                return xId.CompareTo(yId);
            }

            if (xIsNumeric != yIsNumeric)
            {
                return xIsNumeric ? -1 : 1;
            }

            return StringComparer.Ordinal.Compare(x, y);
        }
    }
}
