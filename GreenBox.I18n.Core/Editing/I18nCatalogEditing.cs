using System;

namespace GreenBox.I18n
{
    /// <summary>
    /// Provides operations that safely edit an i18n catalog in memory.
    /// </summary>
    public static class I18nCatalogEditing
    {
        /// <summary>
        /// Changes an entry path while preserving its stable ID and localized values.
        /// </summary>
        /// <param name="catalog">The valid catalog to edit.</param>
        /// <param name="id">The positive ID of the entry to move.</param>
        /// <param name="newPath">The new full logical path.</param>
        /// <returns>The operation result and affected entry.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is null.</exception>
        public static I18nEditResult MoveEntry(this I18nCatalog catalog, long id, string? newPath)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (id <= 0)
            {
                return I18nEditResult.Failure(
                    I18nEditCodes.InvalidId,
                    $"Entry ID must be positive: {id}.");
            }

            if (!I18nPathRules.IsValid(newPath))
            {
                return I18nEditResult.Failure(
                    I18nEditCodes.InvalidPath,
                    "The path must contain dot-separated identifier segments using Latin letters, digits, and underscores.");
            }

            I18nEntry? entry = catalog.FindById(id);
            if (entry == null)
            {
                return I18nEditResult.Failure(
                    I18nEditCodes.EntryNotFound,
                    $"Entry with ID {id} was not found.");
            }

            for (int entryIndex = 0; entryIndex < catalog.Entries.Count; entryIndex++)
            {
                I18nEntry otherEntry = catalog.Entries[entryIndex];
                if (!ReferenceEquals(entry, otherEntry) &&
                    string.Equals(otherEntry.Path, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    return I18nEditResult.Failure(
                        I18nEditCodes.DuplicatePath,
                        $"Entry with ID {otherEntry.Id} already uses path '{otherEntry.Path}'.");
                }
            }

            if (string.Equals(entry.Path, newPath, StringComparison.Ordinal))
            {
                return I18nEditResult.Success(entry, false);
            }

            entry.Path = newPath!;
            catalog.Entries.Sort(I18nEntryComparer.Canonical);
            return I18nEditResult.Success(entry, true);
        }
    }
}
