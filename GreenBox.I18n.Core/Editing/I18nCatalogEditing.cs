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
        /// Adds an empty entry with a random 12-digit stable ID and restores canonical order.
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

            if (!I18nEntryIdAllocator.TryAllocate(catalog.Entries, out long id, out I18nEditError? error))
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
            return I18nEditResult.Success(entry, true);
        }

        /// <summary>
        /// Removes an entry without modifying any other entry IDs.
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

        /// <summary>
        /// Changes several entry paths as one atomic operation.
        /// </summary>
        /// <param name="catalog">The valid catalog to edit.</param>
        /// <param name="moves">The requested entry IDs and destination paths.</param>
        /// <returns>The atomic operation result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static I18nBatchEditResult MoveEntries(
            this I18nCatalog catalog,
            IReadOnlyCollection<I18nEntryMove> moves)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (moves == null)
            {
                throw new ArgumentNullException(nameof(moves));
            }

            var pathsById = new Dictionary<long, string>();
            foreach (I18nEntryMove move in moves)
            {
                if (move.Id <= 0)
                {
                    return I18nBatchEditResult.Failure(
                        I18nEditCodes.InvalidId,
                        $"Entry ID must be positive: {move.Id}.");
                }

                if (!I18nPathRules.IsValid(move.Path))
                {
                    return I18nBatchEditResult.Failure(
                        I18nEditCodes.InvalidPath,
                        "Every path must contain dot-separated identifier segments using Latin letters, digits, and underscores.");
                }

                if (!pathsById.TryAdd(move.Id, move.Path!))
                {
                    return I18nBatchEditResult.Failure(
                        I18nEditCodes.DuplicateEntryMove,
                        $"Entry ID {move.Id} occurs more than once in the move operation.");
                }
            }

            var entriesById = new Dictionary<long, I18nEntry>();
            for (int entryIndex = 0; entryIndex < catalog.Entries.Count; entryIndex++)
            {
                I18nEntry entry = catalog.Entries[entryIndex];
                if (!long.TryParse(
                        entry.Id,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out long entryId) ||
                    entryId <= 0)
                {
                    return I18nBatchEditResult.Failure(
                        I18nEditCodes.InvalidId,
                        $"Entry ID '{entry.Id}' is not a positive 64-bit integer.");
                }

                entriesById.Add(entryId, entry);
            }

            foreach (long id in pathsById.Keys)
            {
                if (!entriesById.ContainsKey(id))
                {
                    return I18nBatchEditResult.Failure(
                        I18nEditCodes.EntryNotFound,
                        $"Entry with ID {id} was not found.");
                }
            }

            var finalPathOwners = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<long, I18nEntry> pair in entriesById)
            {
                string finalPath = pathsById.TryGetValue(pair.Key, out string? movedPath)
                    ? movedPath
                    : pair.Value.Path;
                if (finalPathOwners.TryGetValue(finalPath, out long conflictingId))
                {
                    return I18nBatchEditResult.Failure(
                        I18nEditCodes.DuplicatePath,
                        $"Entries {conflictingId} and {pair.Key} would both use path '{finalPath}'.");
                }

                finalPathOwners.Add(finalPath, pair.Key);
            }

            bool hasChanges = false;
            foreach (KeyValuePair<long, string> pair in pathsById)
            {
                I18nEntry entry = entriesById[pair.Key];
                if (!string.Equals(entry.Path, pair.Value, StringComparison.Ordinal))
                {
                    entry.Path = pair.Value;
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                catalog.Entries.Sort(I18nEntryComparer.Canonical);
            }

            return I18nBatchEditResult.Success(hasChanges);
        }

    }
}
