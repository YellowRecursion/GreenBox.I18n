using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GreenBox.I18n
{
    /// <summary>
    /// Provides read-only operations for locating entries in an i18n catalog.
    /// </summary>
    public static class I18nCatalogQueries
    {
        /// <summary>
        /// Finds an entry by its stable numeric identifier.
        /// </summary>
        /// <param name="catalog">The catalog to search.</param>
        /// <param name="id">The self-identifying entry identifier.</param>
        /// <returns>The matching entry, or <see langword="null"/> when no entry has the identifier.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="id"/> is invalid.</exception>
        public static I18nEntry? FindById(this I18nCatalog catalog, long id)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (!I18nEntryId.IsValid(id))
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "The entry ID format is invalid.");
            }

            string idText = id.ToString(CultureInfo.InvariantCulture);
            return catalog.Entries.FirstOrDefault(entry => entry.Id == idText);
        }

        /// <summary>
        /// Finds an entry by its logical path using the catalog's case-insensitive uniqueness rules.
        /// </summary>
        /// <param name="catalog">The catalog to search.</param>
        /// <param name="path">The complete logical entry path.</param>
        /// <returns>The matching entry, or <see langword="null"/> when no entry has the path.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is invalid.</exception>
        public static I18nEntry? FindByPath(this I18nCatalog catalog, string? path)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (!I18nPathRules.IsValid(path))
            {
                throw new ArgumentException(
                    "The path must contain dot-separated identifier segments using Latin letters, digits, and underscores.",
                    nameof(path));
            }

            return catalog.Entries.FirstOrDefault(entry =>
                string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Searches entry paths, comments, and localized text using a case-insensitive query.
        /// </summary>
        /// <param name="catalog">The catalog to search.</param>
        /// <param name="query">The non-empty text to find.</param>
        /// <returns>A deterministically ordered snapshot of matching entries.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="query"/> is empty or whitespace.</exception>
        public static IReadOnlyList<I18nEntry> Search(this I18nCatalog catalog, string query)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("A search query cannot be empty.", nameof(query));
            }

            return catalog.Entries
                .Select(entry => new SearchMatch(entry, GetMatchRank(entry, query)))
                .Where(match => match.Rank != SearchRank.None)
                .OrderBy(match => match.Rank)
                .ThenBy(match => match.Entry, I18nEntryComparer.Canonical)
                .Select(match => match.Entry)
                .ToArray();
        }

        private static SearchRank GetMatchRank(I18nEntry entry, string query)
        {
            if (string.Equals(entry.Path, query, StringComparison.OrdinalIgnoreCase))
            {
                return SearchRank.ExactPath;
            }

            if (entry.Path.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return SearchRank.PathPrefix;
            }

            if (entry.Path.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return SearchRank.PathContains;
            }

            if (Contains(entry.Comment, query) || entry.Locales.Values.Any(value => Contains(value.Text, query)))
            {
                return SearchRank.ContentContains;
            }

            return SearchRank.None;
        }

        private static bool Contains(string? value, string query)
        {
            return value?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private readonly struct SearchMatch
        {
            public SearchMatch(I18nEntry entry, SearchRank rank)
            {
                Entry = entry;
                Rank = rank;
            }

            public I18nEntry Entry { get; }

            public SearchRank Rank { get; }
        }

        private enum SearchRank
        {
            ExactPath,
            PathPrefix,
            PathContains,
            ContentContains,
            None,
        }
    }
}
