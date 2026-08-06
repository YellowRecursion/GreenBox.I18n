using System;
using System.Collections.Generic;

namespace GreenBox.I18n
{
    /// <summary>
    /// Allocates self-identifying random entry IDs that remain safe across independent catalog copies.
    /// </summary>
    internal static class I18nEntryIdAllocator
    {
        private const int MaximumAttempts = 128;

        /// <summary>
        /// Allocates an unused self-identifying ID using a cryptographically secure random source.
        /// </summary>
        internal static bool TryAllocate(
            IReadOnlyList<I18nEntry> entries,
            out long id,
            out I18nEditError? error)
        {
            var occupiedIds = new HashSet<long>();
            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                string entryIdText = entries[entryIndex].Id;
                if (!I18nEntryId.TryParse(entryIdText, out long entryId))
                {
                    id = 0;
                    error = new I18nEditError(
                        I18nEditCodes.InvalidId,
                        $"Entry ID '{entryIdText}' does not use the GreenBox I18n ID format.");
                    return false;
                }

                occupiedIds.Add(entryId);
            }

            for (int attempt = 0; attempt < MaximumAttempts; attempt++)
            {
                long candidate = I18nEntryId.Generate();
                if (occupiedIds.Add(candidate))
                {
                    id = candidate;
                    error = null;
                    return true;
                }
            }

            id = 0;
            error = new I18nEditError(
                I18nEditCodes.IdSpaceExhausted,
                $"A unique GreenBox I18n entry ID could not be allocated after {MaximumAttempts} attempts.");
            return false;
        }
    }
}
