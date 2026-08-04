using System.Text.RegularExpressions;

namespace GreenBox.I18n
{
    /// <summary>
    /// Defines the syntax accepted for full logical entry paths.
    /// </summary>
    public static class I18nPathRules
    {
        private static readonly Regex PathRegex = new Regex(
            @"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Determines whether a value is a valid dot-separated i18n entry path.
        /// </summary>
        /// <param name="path">The path to examine.</param>
        /// <returns><see langword="true"/> when the path is valid; otherwise, <see langword="false"/>.</returns>
        public static bool IsValid(string? path)
        {
            return !string.IsNullOrWhiteSpace(path) && PathRegex.IsMatch(path);
        }
    }
}
