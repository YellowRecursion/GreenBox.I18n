using System;
using System.Collections.Generic;
using System.Globalization;

namespace GreenBox.I18n
{
    /// <summary>
    /// Provides operations that safely edit an i18n catalog in memory.
    /// </summary>
    public static class I18nCatalogEditing
    {
        /// <summary>
        /// Adds an empty entry with the next short stable ID and restores canonical order.
        /// </summary>
        /// <param name="catalog">The valid catalog to edit.</param>
        /// <param name="path">The new entry's full logical path.</param>
        /// <returns>The operation result and created entry.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is null.</exception>
        public static I18nEditResult AddEntry(this I18nCatalog catalog, string? path)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (!I18nPathRules.IsValid(path))
            {
                return I18nEditResult.Failure(
                    I18nEditCodes.InvalidPath,
                    "The path must contain dot-separated identifier segments using Latin letters, digits, and underscores.");
            }

            for (int entryIndex = 0; entryIndex < catalog.Entries.Count; entryIndex++)
            {
                I18nEntry existingEntry = catalog.Entries[entryIndex];
                if (string.Equals(existingEntry.Path, path, StringComparison.OrdinalIgnoreCase))
                {
                    return I18nEditResult.Failure(
                        I18nEditCodes.DuplicatePath,
                        $"Entry with ID {existingEntry.Id} already uses path '{existingEntry.Path}'.");
                }
            }

            if (!TryGetNextId(catalog, out long id, out I18nEditError? error))
            {
                return I18nEditResult.Failure(error!.Code, error.Message);
            }

            var entry = new I18nEntry
            {
                Id = id.ToString(CultureInfo.InvariantCulture),
                Path = path!,
                Locales = new Dictionary<string, I18nLocaleValue>(),
            };

            catalog.Entries.Add(entry);
            catalog.Entries.Sort(I18nEntryComparer.Canonical);
            catalog.NextId = (id + 1).ToString(CultureInfo.InvariantCulture);
            return I18nEditResult.Success(entry, true);
        }

        /// <summary>
        /// Removes an entry while preserving its ID as consumed by the catalog allocator.
        /// </summary>
        /// <param name="catalog">The valid catalog to edit.</param>
        /// <param name="id">The positive ID of the entry to remove.</param>
        /// <returns>The operation result and removed entry.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is null.</exception>
        public static I18nEditResult RemoveEntry(this I18nCatalog catalog, long id)
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

            I18nEntry? entry = catalog.FindById(id);
            if (entry == null)
            {
                return I18nEditResult.Failure(
                    I18nEditCodes.EntryNotFound,
                    $"Entry with ID {id} was not found.");
            }

            if (catalog.NextId == null)
            {
                if (!TryGetNextId(catalog, out long nextId, out I18nEditError? error))
                {
                    return I18nEditResult.Failure(error!.Code, error.Message);
                }

                catalog.NextId = nextId.ToString(CultureInfo.InvariantCulture);
            }

            catalog.Entries.Remove(entry);
            return I18nEditResult.Success(entry, true);
        }

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

        private static bool TryGetNextId(
            I18nCatalog catalog,
            out long nextId,
            out I18nEditError? error)
        {
            long greatestId = 0;
            for (int entryIndex = 0; entryIndex < catalog.Entries.Count; entryIndex++)
            {
                if (!long.TryParse(
                        catalog.Entries[entryIndex].Id,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out long entryId) ||
                    entryId <= 0)
                {
                    nextId = 0;
                    error = new I18nEditError(
                        I18nEditCodes.InvalidId,
                        $"Entry ID '{catalog.Entries[entryIndex].Id}' is not a positive 64-bit integer.");
                    return false;
                }

                greatestId = Math.Max(greatestId, entryId);
            }

            if (catalog.NextId == null)
            {
                if (greatestId == long.MaxValue)
                {
                    nextId = 0;
                    error = new I18nEditError(
                        I18nEditCodes.IdSpaceExhausted,
                        "No additional positive 64-bit entry ID can be allocated.");
                    return false;
                }

                nextId = greatestId + 1;
            }
            else if (!long.TryParse(
                         catalog.NextId,
                         NumberStyles.None,
                         CultureInfo.InvariantCulture,
                         out nextId) ||
                     nextId <= greatestId)
            {
                error = new I18nEditError(
                    I18nEditCodes.InvalidNextId,
                    "The catalog next ID must be a positive 64-bit integer greater than every existing entry ID.");
                return false;
            }

            if (nextId == long.MaxValue)
            {
                error = new I18nEditError(
                    I18nEditCodes.IdSpaceExhausted,
                    "No additional positive 64-bit entry ID can be allocated.");
                return false;
            }

            error = null;
            return true;
        }
    }
}
