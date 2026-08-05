using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;

namespace GreenBox.I18n
{
    /// <summary>
    /// Allocates compact random entry IDs that remain safe across independent catalog copies.
    /// </summary>
    internal static class I18nEntryIdAllocator
    {
        private const long MinimumId = 100_000_000_000;
        private const long MaximumId = 999_999_999_999;
        private const int MaximumAttempts = 128;
        private const ulong Range = (ulong)(MaximumId - MinimumId + 1);

        /// <summary>
        /// Allocates an unused 12-digit ID using a cryptographically secure random source.
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
                if (!long.TryParse(
                        entryIdText,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out long entryId) ||
                    entryId <= 0)
                {
                    id = 0;
                    error = new I18nEditError(
                        I18nEditCodes.InvalidId,
                        $"Entry ID '{entryIdText}' is not a positive 64-bit integer.");
                    return false;
                }

                occupiedIds.Add(entryId);
            }

            for (int attempt = 0; attempt < MaximumAttempts; attempt++)
            {
                long candidate = GenerateCandidate();
                if (candidate >= MinimumId && candidate <= MaximumId && occupiedIds.Add(candidate))
                {
                    id = candidate;
                    error = null;
                    return true;
                }
            }

            id = 0;
            error = new I18nEditError(
                I18nEditCodes.IdSpaceExhausted,
                $"A unique 12-digit entry ID could not be allocated after {MaximumAttempts} attempts.");
            return false;
        }

        private static long GenerateCandidate()
        {
            var bytes = new byte[sizeof(ulong)];
            using RandomNumberGenerator generator = RandomNumberGenerator.Create();

            ulong remainder = ((ulong.MaxValue % Range) + 1) % Range;
            ulong greatestAcceptedValue = ulong.MaxValue - remainder;
            ulong value;
            do
            {
                generator.GetBytes(bytes);
                value = BitConverter.ToUInt64(bytes, 0);
            }
            while (value > greatestAcceptedValue);

            return MinimumId + (long)(value % Range);
        }
    }
}
