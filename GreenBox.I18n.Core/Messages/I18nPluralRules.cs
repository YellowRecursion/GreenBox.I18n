using System.Globalization;

namespace GreenBox.I18n
{
    internal static class I18nPluralRules
    {
        public static string Select(CultureInfo culture, decimal value, bool ordinal)
        {
            I18nPluralData.PluralCategory category = ordinal
                ? I18nPluralData.SelectOrdinal(culture.Name, value)
                : I18nPluralData.SelectCardinal(culture.Name, value);
            return category switch
            {
                I18nPluralData.PluralCategory.Zero => "zero",
                I18nPluralData.PluralCategory.One => "one",
                I18nPluralData.PluralCategory.Two => "two",
                I18nPluralData.PluralCategory.Few => "few",
                I18nPluralData.PluralCategory.Many => "many",
                _ => "other",
            };
        }
    }
}
