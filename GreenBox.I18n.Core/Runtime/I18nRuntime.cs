using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace GreenBox.I18n
{
    /// <summary>
    /// Provides indexed, locale-aware access to a validated localization catalog.
    /// </summary>
    public sealed class I18nRuntime
    {
        private readonly IReadOnlyDictionary<long, RuntimeEntry> _entriesById;
        private readonly IReadOnlyDictionary<string, RuntimeLocale> _localesById;
        private readonly IReadOnlyList<I18nRuntimeLocale> _locales;
        private readonly string _defaultLocaleId;
        private RuntimeLocale _currentLocale;

        /// <summary>
        /// Initializes a runtime using the catalog default locale.
        /// </summary>
        /// <param name="catalog">The source catalog used to build the runtime snapshot.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is null.</exception>
        /// <exception cref="I18nInvalidCatalogException">Thrown when the catalog contains validation errors.</exception>
        public I18nRuntime(I18nCatalog catalog)
            : this(catalog, null, true)
        {
        }

        /// <summary>
        /// Initializes a runtime using the specified locale.
        /// </summary>
        /// <param name="catalog">The source catalog used to build the runtime snapshot.</param>
        /// <param name="localeId">The initially selected locale identifier.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> or <paramref name="localeId"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="localeId"/> is not declared by the catalog.</exception>
        /// <exception cref="I18nInvalidCatalogException">Thrown when the catalog contains validation errors.</exception>
        public I18nRuntime(I18nCatalog catalog, string localeId)
            : this(catalog, localeId ?? throw new ArgumentNullException(nameof(localeId)), false)
        {
        }

        private I18nRuntime(I18nCatalog catalog, string? localeId, bool useDefaultLocale)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            I18nValidationResult validationResult = I18nCatalogValidator.Validate(catalog);
            if (validationResult.HasErrors)
            {
                throw new I18nInvalidCatalogException(validationResult);
            }

            _defaultLocaleId = catalog.DefaultLocale;
            _localesById = BuildLocales(catalog.Locales, catalog.DefaultLocale, out _locales);
            _entriesById = BuildEntries(catalog.Entries);

            string initialLocaleId = useDefaultLocale ? _defaultLocaleId : localeId!;
            if (!_localesById.TryGetValue(initialLocaleId, out RuntimeLocale? initialLocale))
            {
                throw new ArgumentException(
                    $"Locale '{initialLocaleId}' is not declared by the catalog.",
                    nameof(localeId));
            }

            _currentLocale = initialLocale;
        }

        /// <summary>
        /// Occurs after the current locale changes.
        /// </summary>
        public event EventHandler<I18nLocaleChangedEventArgs>? LocaleChanged;

        /// <summary>
        /// Gets the locales in their catalog display order.
        /// </summary>
        public IReadOnlyList<I18nRuntimeLocale> Locales => _locales;

        /// <summary>
        /// Gets the catalog default locale.
        /// </summary>
        public I18nRuntimeLocale DefaultLocale => _localesById[_defaultLocaleId].PublicLocale;

        /// <summary>
        /// Gets the currently selected locale.
        /// </summary>
        public I18nRuntimeLocale CurrentLocale => _currentLocale.PublicLocale;

        /// <summary>
        /// Gets the culture of the currently selected locale.
        /// </summary>
        public CultureInfo CurrentCulture => _currentLocale.PublicLocale.Culture;

        /// <summary>
        /// Changes the current locale.
        /// </summary>
        /// <param name="localeId">The declared locale identifier to select.</param>
        /// <returns><see langword="true"/> when the locale changed; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="localeId"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the locale is not declared by the catalog.</exception>
        public bool SetLocale(string localeId)
        {
            if (localeId == null)
            {
                throw new ArgumentNullException(nameof(localeId));
            }

            if (!_localesById.TryGetValue(localeId, out RuntimeLocale? locale))
            {
                throw new ArgumentException(
                    $"Locale '{localeId}' is not declared by the catalog.",
                    nameof(localeId));
            }

            if (ReferenceEquals(_currentLocale, locale))
            {
                return false;
            }

            I18nRuntimeLocale previousLocale = _currentLocale.PublicLocale;
            _currentLocale = locale;
            LocaleChanged?.Invoke(
                this,
                new I18nLocaleChangedEventArgs(previousLocale, _currentLocale.PublicLocale));
            return true;
        }

        /// <summary>
        /// Gets localized text, falling back to the entry path or numeric ID when no text exists.
        /// </summary>
        /// <param name="id">The positive stable entry ID.</param>
        /// <returns>The resolved text, entry path, or numeric ID.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="id"/> is not positive.</exception>
        public string Text(long id)
        {
            EnsurePositiveId(id);

            if (TryGetText(id, out string? text))
            {
                return text!;
            }

            return _entriesById.TryGetValue(id, out RuntimeEntry? entry)
                ? entry.Path
                : id.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats localized text using the current locale culture.
        /// </summary>
        /// <param name="id">The positive stable entry ID.</param>
        /// <param name="arguments">The values inserted into the localized composite format string.</param>
        /// <returns>The formatted localized text.</returns>
        public string Format(long id, params object?[] arguments)
        {
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            return string.Format(CurrentCulture, Text(id), arguments);
        }

        /// <summary>
        /// Attempts to resolve localized text through the current locale fallback chain.
        /// </summary>
        /// <param name="id">The positive stable entry ID.</param>
        /// <param name="text">The resolved text when one exists.</param>
        /// <returns><see langword="true"/> when text was found; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="id"/> is not positive.</exception>
        public bool TryGetText(long id, out string? text)
        {
            EnsurePositiveId(id);
            text = null;

            if (!_entriesById.TryGetValue(id, out RuntimeEntry? entry))
            {
                return false;
            }

            IReadOnlyList<string> fallbackChain = _currentLocale.FallbackChain;
            for (int localeIndex = 0; localeIndex < fallbackChain.Count; localeIndex++)
            {
                if (entry.ValuesByLocale.TryGetValue(fallbackChain[localeIndex], out RuntimeValue? value) &&
                    value.Text != null)
                {
                    text = value.Text;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets an asset reference through the current locale fallback chain.
        /// </summary>
        /// <param name="id">The positive stable entry ID.</param>
        /// <returns>The resolved asset reference, or <see langword="null"/> when no asset exists.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="id"/> is not positive.</exception>
        public I18nAssetReference? Asset(long id)
        {
            TryGetAsset(id, out I18nAssetReference? asset);
            return asset;
        }

        /// <summary>
        /// Attempts to resolve an asset reference through the current locale fallback chain.
        /// </summary>
        /// <param name="id">The positive stable entry ID.</param>
        /// <param name="asset">The resolved asset reference when one exists.</param>
        /// <returns><see langword="true"/> when an asset was found; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="id"/> is not positive.</exception>
        public bool TryGetAsset(long id, out I18nAssetReference? asset)
        {
            EnsurePositiveId(id);
            asset = null;

            if (!_entriesById.TryGetValue(id, out RuntimeEntry? entry))
            {
                return false;
            }

            IReadOnlyList<string> fallbackChain = _currentLocale.FallbackChain;
            for (int localeIndex = 0; localeIndex < fallbackChain.Count; localeIndex++)
            {
                if (entry.ValuesByLocale.TryGetValue(fallbackChain[localeIndex], out RuntimeValue? value) &&
                    value.Asset != null)
                {
                    asset = CloneAsset(value.Asset);
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyDictionary<string, RuntimeLocale> BuildLocales(
            IReadOnlyList<I18nLocaleDefinition> definitions,
            string defaultLocaleId,
            out IReadOnlyList<I18nRuntimeLocale> publicLocales)
        {
            var mutableLocales = new Dictionary<string, RuntimeLocale>(StringComparer.Ordinal);
            var mutablePublicLocales = new List<I18nRuntimeLocale>(definitions.Count);

            for (int localeIndex = 0; localeIndex < definitions.Count; localeIndex++)
            {
                I18nLocaleDefinition definition = definitions[localeIndex];
                var publicLocale = new I18nRuntimeLocale(definition);
                mutableLocales.Add(
                    definition.Id,
                    new RuntimeLocale(publicLocale, BuildFallbackChain(definition, definitions, defaultLocaleId)));
                mutablePublicLocales.Add(publicLocale);
            }

            publicLocales = mutablePublicLocales.AsReadOnly();
            return new ReadOnlyDictionary<string, RuntimeLocale>(mutableLocales);
        }

        private static IReadOnlyList<string> BuildFallbackChain(
            I18nLocaleDefinition definition,
            IReadOnlyList<I18nLocaleDefinition> definitions,
            string defaultLocaleId)
        {
            var definitionsById = new Dictionary<string, I18nLocaleDefinition>(StringComparer.Ordinal);
            for (int localeIndex = 0; localeIndex < definitions.Count; localeIndex++)
            {
                definitionsById.Add(definitions[localeIndex].Id, definitions[localeIndex]);
            }

            var chain = new List<string> { definition.Id };
            I18nLocaleDefinition current = definition;

            while (current.Fallback != null)
            {
                chain.Add(current.Fallback);
                current = definitionsById[current.Fallback];
            }

            if (!chain.Contains(defaultLocaleId))
            {
                chain.Add(defaultLocaleId);
            }

            return chain.AsReadOnly();
        }

        private static IReadOnlyDictionary<long, RuntimeEntry> BuildEntries(IReadOnlyList<I18nEntry> entries)
        {
            var result = new Dictionary<long, RuntimeEntry>();

            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                I18nEntry sourceEntry = entries[entryIndex];
                var values = new Dictionary<string, RuntimeValue>(StringComparer.Ordinal);

                foreach (KeyValuePair<string, I18nLocaleValue> pair in sourceEntry.Locales)
                {
                    values.Add(pair.Key, new RuntimeValue(pair.Value.Text, CloneAsset(pair.Value.Asset)));
                }

                long id = long.Parse(sourceEntry.Id, NumberStyles.None, CultureInfo.InvariantCulture);
                result.Add(
                    id,
                    new RuntimeEntry(
                        sourceEntry.Path,
                        new ReadOnlyDictionary<string, RuntimeValue>(values)));
            }

            return new ReadOnlyDictionary<long, RuntimeEntry>(result);
        }

        private static I18nAssetReference? CloneAsset(I18nAssetReference? asset)
        {
            if (asset == null)
            {
                return null;
            }

            return new I18nAssetReference
            {
                AssetGuid = asset.AssetGuid,
                LocalFileId = asset.LocalFileId,
            };
        }

        private static void EnsurePositiveId(long id)
        {
            if (id <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Entry ID must be positive.");
            }
        }

        private sealed class RuntimeLocale
        {
            public RuntimeLocale(I18nRuntimeLocale publicLocale, IReadOnlyList<string> fallbackChain)
            {
                PublicLocale = publicLocale;
                FallbackChain = fallbackChain;
            }

            public I18nRuntimeLocale PublicLocale { get; }

            public IReadOnlyList<string> FallbackChain { get; }
        }

        private sealed class RuntimeEntry
        {
            public RuntimeEntry(string path, IReadOnlyDictionary<string, RuntimeValue> valuesByLocale)
            {
                Path = path;
                ValuesByLocale = valuesByLocale;
            }

            public string Path { get; }

            public IReadOnlyDictionary<string, RuntimeValue> ValuesByLocale { get; }
        }

        private sealed class RuntimeValue
        {
            public RuntimeValue(string? text, I18nAssetReference? asset)
            {
                Text = text;
                Asset = asset;
            }

            public string? Text { get; }

            public I18nAssetReference? Asset { get; }
        }
    }
}
