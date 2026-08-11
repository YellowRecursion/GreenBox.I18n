using System;
using System.Text;

namespace GreenBox.I18n
{
    internal static class I18nUnicode
    {
        public static string NormalizeNfc(string value)
        {
            return value.IsNormalized(NormalizationForm.FormC)
                ? value
                : value.Normalize(NormalizationForm.FormC);
        }

        public static bool EqualsNfc(string? left, string? right)
        {
            if (string.Equals(left, right, StringComparison.Ordinal))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(
                NormalizeNfc(left),
                NormalizeNfc(right),
                StringComparison.Ordinal);
        }
    }
}
