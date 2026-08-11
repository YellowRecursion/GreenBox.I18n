using System;
using System.Globalization;

namespace GreenBox.I18n
{
    internal static partial class I18nPluralData
    {
        public static PluralCategory SelectCardinal(string cultureName, decimal value)
        {
            ushort ruleSetId = FindRuleSet(cultureName, cardinal: true);
            return Evaluate(ruleSetId, PluralNumber.FromDecimal(value));
        }

        public static PluralCategory SelectOrdinal(string cultureName, decimal value)
        {
            ushort ruleSetId = FindRuleSet(cultureName, cardinal: false);
            return Evaluate(ruleSetId, PluralNumber.FromDecimal(value));
        }

        internal static PluralCategory SelectCldrSample(
            string cultureName,
            bool ordinal,
            string sample)
        {
            ushort ruleSetId = FindRuleSet(cultureName, cardinal: !ordinal);
            return Evaluate(ruleSetId, PluralNumber.FromCldrSample(sample));
        }

        private static ushort FindRuleSet(string cultureName, bool cardinal)
        {
            ReadOnlySpan<char> candidate = cultureName.AsSpan();
            while (candidate.Length > 0)
            {
                int low = 0;
                int high = Locales.Length - 1;
                while (low <= high)
                {
                    int middle = low + ((high - low) / 2);
                    int comparison = Locales[middle].Locale.AsSpan().SequenceCompareTo(candidate);
                    if (comparison == 0)
                    {
                        return cardinal
                            ? Locales[middle].CardinalRuleSet
                            : Locales[middle].OrdinalRuleSet;
                    }

                    if (comparison < 0)
                    {
                        low = middle + 1;
                    }
                    else
                    {
                        high = middle - 1;
                    }
                }

                int separator = candidate.LastIndexOf('-');
                if (separator < 0)
                {
                    break;
                }

                candidate = candidate.Slice(0, separator);
            }

            return 0;
        }

        private static PluralCategory Evaluate(ushort ruleSetId, PluralNumber number)
        {
            RuleSet ruleSet = RuleSets[ruleSetId];
            int categoryEnd = ruleSet.CategoryOffset + ruleSet.CategoryCount;
            for (int categoryIndex = ruleSet.CategoryOffset; categoryIndex < categoryEnd; categoryIndex++)
            {
                CategoryRule category = Categories[categoryIndex];
                int groupEnd = category.GroupOffset + category.GroupCount;
                for (int groupIndex = category.GroupOffset; groupIndex < groupEnd; groupIndex++)
                {
                    if (EvaluateGroup(Groups[groupIndex], number))
                    {
                        return category.Category;
                    }
                }
            }

            return PluralCategory.Other;
        }

        private static bool EvaluateGroup(AndGroup group, PluralNumber number)
        {
            int relationEnd = group.RelationOffset + group.RelationCount;
            for (int relationIndex = group.RelationOffset; relationIndex < relationEnd; relationIndex++)
            {
                if (!EvaluateRelation(Relations[relationIndex], number))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool EvaluateRelation(Relation relation, PluralNumber number)
        {
            decimal operand = number.Get(relation.Operand);
            if (relation.Modulo != 0)
            {
                operand %= relation.Modulo;
            }

            // CLDR's preferred '=' / '!=' relations have the semantics of
            // 'in': only integer numeric values can belong to their ranges.
            bool matches = false;
            int rangeEnd = relation.RangeOffset + relation.RangeCount;
            if (operand == decimal.Truncate(operand))
            {
                for (int rangeIndex = relation.RangeOffset; rangeIndex < rangeEnd; rangeIndex++)
                {
                    Range range = Ranges[rangeIndex];
                    if (operand >= range.Start && operand <= range.End)
                    {
                        matches = true;
                        break;
                    }
                }
            }

            return relation.Negated ? !matches : matches;
        }

        internal enum PluralCategory : byte
        {
            Zero,
            One,
            Two,
            Few,
            Many,
            Other,
        }

        internal enum PluralOperand : byte
        {
            N,
            I,
            V,
            W,
            F,
            T,
            C,
            E,
        }

        internal readonly struct LocaleMap
        {
            public LocaleMap(string locale, ushort cardinalRuleSet, ushort ordinalRuleSet)
            {
                Locale = locale;
                CardinalRuleSet = cardinalRuleSet;
                OrdinalRuleSet = ordinalRuleSet;
            }

            public string Locale { get; }
            public ushort CardinalRuleSet { get; }
            public ushort OrdinalRuleSet { get; }
        }

        internal readonly struct RuleSet
        {
            public RuleSet(int categoryOffset, int categoryCount)
            {
                CategoryOffset = categoryOffset;
                CategoryCount = categoryCount;
            }

            public int CategoryOffset { get; }
            public int CategoryCount { get; }
        }

        internal readonly struct CategoryRule
        {
            public CategoryRule(PluralCategory category, int groupOffset, int groupCount)
            {
                Category = category;
                GroupOffset = groupOffset;
                GroupCount = groupCount;
            }

            public PluralCategory Category { get; }
            public int GroupOffset { get; }
            public int GroupCount { get; }
        }

        internal readonly struct AndGroup
        {
            public AndGroup(int relationOffset, int relationCount)
            {
                RelationOffset = relationOffset;
                RelationCount = relationCount;
            }

            public int RelationOffset { get; }
            public int RelationCount { get; }
        }

        internal readonly struct Relation
        {
            public Relation(
                PluralOperand operand,
                int modulo,
                bool negated,
                int rangeOffset,
                int rangeCount)
            {
                Operand = operand;
                Modulo = modulo;
                Negated = negated;
                RangeOffset = rangeOffset;
                RangeCount = rangeCount;
            }

            public PluralOperand Operand { get; }
            public int Modulo { get; }
            public bool Negated { get; }
            public int RangeOffset { get; }
            public int RangeCount { get; }
        }

        internal readonly struct Range
        {
            public Range(decimal start, decimal end)
            {
                Start = start;
                End = end;
            }

            public decimal Start { get; }
            public decimal End { get; }
        }

        private readonly struct PluralNumber
        {
            private PluralNumber(
                decimal n,
                decimal i,
                int v,
                int w,
                decimal f,
                decimal t,
                int c,
                int e)
            {
                N = n;
                I = i;
                V = v;
                W = w;
                F = f;
                T = t;
                C = c;
                E = e;
            }

            private decimal N { get; }
            private decimal I { get; }
            private int V { get; }
            private int W { get; }
            private decimal F { get; }
            private decimal T { get; }
            private int C { get; }
            private int E { get; }

            public static PluralNumber FromDecimal(decimal value)
            {
                decimal n = Math.Abs(value);
                int[] bits = decimal.GetBits(n);
                int scale = (bits[3] >> 16) & 0x7F;
                return FromParts(n, scale, 0, 0);
            }

            private static PluralNumber FromParts(decimal n, int scale, int c, int e)
            {
                decimal i = decimal.Truncate(n);
                decimal factor = Pow10(scale);
                decimal f = decimal.Truncate((n - i) * factor);
                decimal t = f;
                int w = scale;
                while (w > 0 && t % 10m == 0m)
                {
                    t /= 10m;
                    w--;
                }

                return new PluralNumber(n, i, scale, w, f, t, c, e);
            }

            public static PluralNumber FromCldrSample(string sample)
            {
                int compactMarker = sample.IndexOf('c');
                if (compactMarker < 0)
                {
                    return FromDecimal(decimal.Parse(sample, CultureInfo.InvariantCulture));
                }

                string mantissaSource = sample.Substring(0, compactMarker);
                decimal mantissa = decimal.Parse(
                    mantissaSource,
                    CultureInfo.InvariantCulture);
                int exponent = int.Parse(
                    sample.Substring(compactMarker + 1),
                    CultureInfo.InvariantCulture);
                decimal expanded = Math.Abs(mantissa) * Pow10(exponent);
                int point = mantissaSource.IndexOf('.');
                int mantissaScale = point < 0 ? 0 : mantissaSource.Length - point - 1;
                int expandedScale = Math.Max(0, mantissaScale - exponent);
                return FromParts(expanded, expandedScale, exponent, exponent);
            }

            public decimal Get(PluralOperand operand)
            {
                return operand switch
                {
                    PluralOperand.N => N,
                    PluralOperand.I => I,
                    PluralOperand.V => V,
                    PluralOperand.W => W,
                    PluralOperand.F => F,
                    PluralOperand.T => T,
                    PluralOperand.C => C,
                    PluralOperand.E => E,
                    _ => throw new ArgumentOutOfRangeException(nameof(operand)),
                };
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
        }
    }
}
