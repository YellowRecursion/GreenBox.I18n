#nullable enable

using System;
using System.Globalization;

namespace GreenBox.I18n.Unity.Text
{
    /// <summary>
    /// Defines an optional culture-aware transformation applied to localized text.
    /// </summary>
    public enum I18nTextTransform
    {
        /// <summary>
        /// Leaves the localized text unchanged.
        /// </summary>
        None,

        /// <summary>
        /// Converts the localized text to uppercase.
        /// </summary>
        Uppercase,

        /// <summary>
        /// Converts the localized text to lowercase.
        /// </summary>
        Lowercase,

        /// <summary>
        /// Converts the first text element to uppercase without changing the remainder.
        /// </summary>
        CapitalizeFirst,

        /// <summary>
        /// Converts the localized text to title case.
        /// </summary>
        TitleCase
    }

    /// <summary>
    /// Applies presentation transformations to localized text.
    /// </summary>
    public static class I18nTextTransformUtility
    {
        /// <summary>
        /// Applies a text transformation using the supplied locale culture.
        /// </summary>
        /// <param name="text">Localized text to transform.</param>
        /// <param name="transform">Transformation to apply.</param>
        /// <param name="culture">Locale culture used for casing rules.</param>
        /// <returns>The transformed text.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="text"/> or <paramref name="culture"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="transform"/> is not recognized.
        /// </exception>
        public static string Apply(
            string text,
            I18nTextTransform transform,
            CultureInfo culture)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (culture == null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            return transform switch
            {
                I18nTextTransform.None => text,
                I18nTextTransform.Uppercase => text.ToUpper(culture),
                I18nTextTransform.Lowercase => text.ToLower(culture),
                I18nTextTransform.CapitalizeFirst => CapitalizeFirst(text, culture),
                I18nTextTransform.TitleCase => culture.TextInfo.ToTitleCase(text),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(transform),
                    transform,
                    "Unknown localized text transformation.")
            };
        }

        private static string CapitalizeFirst(string text, CultureInfo culture)
        {
            if (text.Length == 0)
            {
                return text;
            }

            string firstTextElement = StringInfo.GetNextTextElement(text);
            return firstTextElement.ToUpper(culture) + text.Substring(firstTextElement.Length);
        }
    }
}
