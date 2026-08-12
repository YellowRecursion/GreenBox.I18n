using System;

namespace GreenBox.I18n
{
    /// <summary>
    /// Provides safe operations for localized values and entry metadata.
    /// </summary>
    public static class I18nCatalogValueEditing
    {
        /// <summary>
        /// Changes an entry comment without affecting its stable identity or localized values.
        /// </summary>
        public static I18nEditResult SetEntryComment(
            this I18nCatalog catalog,
            long id,
            string? comment)
        {
            I18nEditResult lookup = FindEditableEntry(catalog, id);
            if (!lookup.IsSuccess)
            {
                return lookup;
            }

            I18nEntry entry = lookup.Entry!;
            if (string.Equals(entry.Comment, comment, StringComparison.Ordinal))
            {
                return I18nEditResult.Success(entry, false);
            }

            entry.Comment = comment;
            return I18nEditResult.Success(entry, true);
        }

        /// <summary>
        /// Sets or clears localized text for one declared locale.
        /// </summary>
        public static I18nEditResult SetEntryText(
            this I18nCatalog catalog,
            long id,
            string? localeId,
            string? text)
        {
            I18nEditResult lookup = FindEditableEntry(catalog, id);
            if (!lookup.IsSuccess)
            {
                return lookup;
            }

            I18nEditResult localeValidation = ValidateLocale(catalog, lookup.Entry!, localeId);
            if (!localeValidation.IsSuccess)
            {
                return localeValidation;
            }

            I18nEntry entry = lookup.Entry!;
            entry.Locales.TryGetValue(localeId!, out I18nLocaleValue? value);
            if (string.Equals(value?.Text, text, StringComparison.Ordinal))
            {
                return I18nEditResult.Success(entry, false);
            }

            value ??= GetOrAddLocaleValue(entry, localeId!);
            value.Text = text;
            RemoveEmptyLocaleValue(entry, localeId!, value);
            return I18nEditResult.Success(entry, true);
        }

        /// <summary>
        /// Sets or clears a localized engine-independent asset reference for one declared locale.
        /// </summary>
        public static I18nEditResult SetEntryAsset(
            this I18nCatalog catalog,
            long id,
            string? localeId,
            I18nAssetReference? asset)
        {
            I18nEditResult lookup = FindEditableEntry(catalog, id);
            if (!lookup.IsSuccess)
            {
                return lookup;
            }

            I18nEditResult localeValidation = ValidateLocale(catalog, lookup.Entry!, localeId);
            if (!localeValidation.IsSuccess)
            {
                return localeValidation;
            }

            I18nEntry entry = lookup.Entry!;
            entry.Locales.TryGetValue(localeId!, out I18nLocaleValue? value);
            if (AssetReferencesEqual(value?.Asset, asset))
            {
                return I18nEditResult.Success(entry, false);
            }

            value ??= GetOrAddLocaleValue(entry, localeId!);
            value.Asset = CloneAssetReference(asset);
            RemoveEmptyLocaleValue(entry, localeId!, value);
            return I18nEditResult.Success(entry, true);
        }

        private static I18nEditResult FindEditableEntry(I18nCatalog catalog, long id)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (!I18nEntryId.IsValid(id))
            {
                return I18nEditResult.Failure(
                    I18nEditCodes.InvalidId,
                    $"Entry ID format is invalid: {id}.");
            }

            I18nEntry? entry = catalog.FindById(id);
            return entry == null
                ? I18nEditResult.Failure(
                    I18nEditCodes.EntryNotFound,
                    $"Entry with ID {id} was not found.")
                : I18nEditResult.Success(entry, false);
        }

        private static I18nEditResult ValidateLocale(
            I18nCatalog catalog,
            I18nEntry entry,
            string? localeId)
        {
            if (catalog.Locales != null)
            {
                for (int localeIndex = 0; localeIndex < catalog.Locales.Count; localeIndex++)
                {
                    I18nLocaleDefinition? locale = catalog.Locales[localeIndex];
                    if (locale != null &&
                        string.Equals(locale.Id, localeId, StringComparison.Ordinal))
                    {
                        return I18nEditResult.Success(entry, false);
                    }
                }
            }

            return I18nEditResult.Failure(
                I18nEditCodes.UnknownLocale,
                $"Locale '{localeId ?? string.Empty}' is not declared by the catalog.");
        }

        private static I18nLocaleValue GetOrAddLocaleValue(I18nEntry entry, string localeId)
        {
            var value = new I18nLocaleValue();
            entry.Locales.Add(localeId, value);
            return value;
        }

        private static void RemoveEmptyLocaleValue(
            I18nEntry entry,
            string localeId,
            I18nLocaleValue value)
        {
            if (value.Text == null && value.Asset == null)
            {
                entry.Locales.Remove(localeId);
            }
        }

        private static bool AssetReferencesEqual(
            I18nAssetReference? left,
            I18nAssetReference? right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            return
                string.Equals(left.AssetGuid, right.AssetGuid, StringComparison.Ordinal) &&
                string.Equals(left.LocalFileId, right.LocalFileId, StringComparison.Ordinal);
        }

        private static I18nAssetReference? CloneAssetReference(I18nAssetReference? source)
        {
            return source == null
                ? null
                : new I18nAssetReference
                {
                    AssetGuid = source.AssetGuid,
                    LocalFileId = source.LocalFileId,
                };
        }
    }
}
