using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GreenBox.I18n
{
    /// <summary>
    /// Validates the structural integrity of an i18n catalog.
    /// </summary>
    public static class I18nCatalogValidator
    {
        /// <summary>
        /// The newest data contract version supported by this validator.
        /// </summary>
        public const int CurrentSchemaVersion = 1;

        private static readonly Regex PathRegex = new(
            @"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex AssetGuidRegex = new(
            "^[a-fA-F0-9]{32}$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Validates a catalog without modifying it.
        /// </summary>
        /// <param name="catalog">The deserialized source catalog to validate.</param>
        /// <returns>An immutable result containing every discovered diagnostic.</returns>
        public static I18nValidationResult Validate(I18nCatalog? catalog)
        {
            var diagnostics = new List<I18nValidationDiagnostic>();

            if (catalog == null)
            {
                AddError(
                    diagnostics,
                    I18nValidationCodes.NullCatalog,
                    "$",
                    "The catalog cannot be null.");
                return new I18nValidationResult(diagnostics);
            }

            if (catalog.SchemaVersion != CurrentSchemaVersion)
            {
                AddError(
                    diagnostics,
                    I18nValidationCodes.UnsupportedSchemaVersion,
                    "$.schemaVersion",
                    $"Schema version {catalog.SchemaVersion} is not supported. Expected {CurrentSchemaVersion}.");
            }

            if (catalog.Entries == null)
            {
                AddError(
                    diagnostics,
                    I18nValidationCodes.NullEntries,
                    "$.entries",
                    "The entries collection cannot be null.");
                return new I18nValidationResult(diagnostics);
            }

            var entryIndexesById = new Dictionary<long, int>();
            var entryIndexesByPath = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

            for (int entryIndex = 0; entryIndex < catalog.Entries.Count; entryIndex++)
            {
                ValidateEntry(
                    catalog.Entries[entryIndex],
                    entryIndex,
                    entryIndexesById,
                    entryIndexesByPath,
                    diagnostics);
            }

            if (!ContainsErrors(diagnostics))
            {
                ValidateEntryOrder(catalog.Entries, diagnostics);
            }

            return new I18nValidationResult(diagnostics);
        }

        private static void ValidateEntryOrder(
            List<I18nEntry> entries,
            List<I18nValidationDiagnostic> diagnostics)
        {
            for (int entryIndex = 1; entryIndex < entries.Count; entryIndex++)
            {
                I18nEntry previous = entries[entryIndex - 1];
                I18nEntry current = entries[entryIndex];

                if (I18nEntryComparer.Canonical.Compare(previous, current) <= 0)
                {
                    continue;
                }

                AddError(
                    diagnostics,
                    I18nValidationCodes.EntriesNotSorted,
                    $"$.entries[{entryIndex}]",
                    $"Entry '{current.Path}' must appear before '{previous.Path}'.");
                return;
            }
        }

        private static bool ContainsErrors(List<I18nValidationDiagnostic> diagnostics)
        {
            for (int diagnosticIndex = 0; diagnosticIndex < diagnostics.Count; diagnosticIndex++)
            {
                if (diagnostics[diagnosticIndex].Severity == I18nValidationSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateEntry(
            I18nEntry entry,
            int entryIndex,
            Dictionary<long, int> entryIndexesById,
            Dictionary<string, int> entryIndexesByPath,
            List<I18nValidationDiagnostic> diagnostics)
        {
            string entryPath = $"$.entries[{entryIndex}]";

            if (entry == null)
            {
                AddError(
                    diagnostics,
                    I18nValidationCodes.NullEntry,
                    entryPath,
                    "An entry cannot be null.");
                return;
            }

            ValidateId(entry.Id, entryIndex, entryPath, entryIndexesById, diagnostics);
            ValidatePath(entry.Path, entryIndex, entryPath, entryIndexesByPath, diagnostics);
            ValidateLocales(entry.Locales, entryPath, diagnostics);
        }

        private static void ValidateId(
            string id,
            int entryIndex,
            string entryPath,
            Dictionary<long, int> entryIndexesById,
            List<I18nValidationDiagnostic> diagnostics)
        {
            string jsonPath = entryPath + ".id";

            if (string.IsNullOrWhiteSpace(id))
            {
                AddError(diagnostics, I18nValidationCodes.MissingId, jsonPath, "The entry ID is required.");
                return;
            }

            if (!long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out long parsedId) || parsedId <= 0)
            {
                AddError(
                    diagnostics,
                    I18nValidationCodes.InvalidId,
                    jsonPath,
                    "The entry ID must be a positive 64-bit integer written using decimal digits.");
                return;
            }

            if (entryIndexesById.TryGetValue(parsedId, out int firstEntryIndex))
            {
                AddError(
                    diagnostics,
                    I18nValidationCodes.DuplicateId,
                    jsonPath,
                    $"The entry ID is already used by $.entries[{firstEntryIndex}].");
                return;
            }

            entryIndexesById.Add(parsedId, entryIndex);
        }

        private static void ValidatePath(
            string path,
            int entryIndex,
            string entryPath,
            Dictionary<string, int> entryIndexesByPath,
            List<I18nValidationDiagnostic> diagnostics)
        {
            string jsonPath = entryPath + ".path";

            if (string.IsNullOrWhiteSpace(path))
            {
                AddError(diagnostics, I18nValidationCodes.MissingPath, jsonPath, "The entry path is required.");
                return;
            }

            if (!PathRegex.IsMatch(path))
            {
                AddError(
                    diagnostics,
                    I18nValidationCodes.InvalidPath,
                    jsonPath,
                    "The path must contain dot-separated identifier segments using Latin letters, digits, and underscores.");
                return;
            }

            if (entryIndexesByPath.TryGetValue(path, out int firstEntryIndex))
            {
                AddError(
                    diagnostics,
                    I18nValidationCodes.DuplicatePath,
                    jsonPath,
                    $"The entry path is already used by $.entries[{firstEntryIndex}].");
                return;
            }

            entryIndexesByPath.Add(path, entryIndex);
        }

        private static void ValidateLocales(
            Dictionary<string, I18nLocaleValue> locales,
            string entryPath,
            List<I18nValidationDiagnostic> diagnostics)
        {
            string localesPath = entryPath + ".locales";

            if (locales == null || locales.Count == 0)
            {
                AddWarning(
                    diagnostics,
                    I18nValidationCodes.MissingLocales,
                    localesPath,
                    "The entry does not contain values for any locale.");
                return;
            }

            var localeIds = new List<string>(locales.Keys);
            localeIds.Sort(System.StringComparer.Ordinal);

            for (int localeIndex = 0; localeIndex < localeIds.Count; localeIndex++)
            {
                string localeId = localeIds[localeIndex];
                I18nLocaleValue localeValue = locales[localeId];
                string localePath = localesPath + "['" + localeId + "']";

                if (string.IsNullOrWhiteSpace(localeId))
                {
                    AddError(
                        diagnostics,
                        I18nValidationCodes.InvalidLocaleId,
                        localesPath,
                        "A locale identifier cannot be empty.");
                    continue;
                }

                if (localeValue == null)
                {
                    AddError(
                        diagnostics,
                        I18nValidationCodes.NullLocaleValue,
                        localePath,
                        "A locale value cannot be null.");
                    continue;
                }

                if (localeValue.Text == null && localeValue.Asset == null)
                {
                    AddWarning(
                        diagnostics,
                        I18nValidationCodes.EmptyLocaleValue,
                        localePath,
                        "The locale value contains neither text nor an asset reference.");
                }

                ValidateAsset(localeValue.Asset, localePath + ".asset", diagnostics);
            }
        }

        private static void ValidateAsset(
            I18nAssetReference? asset,
            string assetPath,
            List<I18nValidationDiagnostic> diagnostics)
        {
            if (asset == null)
            {
                return;
            }

            string guidPath = assetPath + ".assetGuid";

            if (string.IsNullOrWhiteSpace(asset.AssetGuid))
            {
                AddError(
                    diagnostics,
                    I18nValidationCodes.MissingAssetGuid,
                    guidPath,
                    "The asset GUID is required when an asset reference is present.");
                return;
            }

            if (!AssetGuidRegex.IsMatch(asset.AssetGuid))
            {
                AddError(
                    diagnostics,
                    I18nValidationCodes.InvalidAssetGuid,
                    guidPath,
                    "The asset GUID must contain exactly 32 hexadecimal characters.");
            }

            ValidateAssetLocalFileId(asset.LocalFileId, assetPath, diagnostics);
        }

        private static void ValidateAssetLocalFileId(
            string? localFileId,
            string assetPath,
            List<I18nValidationDiagnostic> diagnostics)
        {
            if (localFileId == null)
            {
                return;
            }

            if (!long.TryParse(
                    localFileId,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out _))
            {
                AddError(
                    diagnostics,
                    I18nValidationCodes.InvalidAssetLocalFileId,
                    assetPath + ".localFileId",
                    "The asset local file ID must be a 64-bit integer written using decimal digits.");
            }
        }

        private static void AddWarning(
            List<I18nValidationDiagnostic> diagnostics,
            string code,
            string jsonPath,
            string message)
        {
            diagnostics.Add(new I18nValidationDiagnostic(
                code,
                I18nValidationSeverity.Warning,
                jsonPath,
                message));
        }

        private static void AddError(
            List<I18nValidationDiagnostic> diagnostics,
            string code,
            string jsonPath,
            string message)
        {
            diagnostics.Add(new I18nValidationDiagnostic(
                code,
                I18nValidationSeverity.Error,
                jsonPath,
                message));
        }
    }
}
