using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Threading;

namespace GreenBox.I18n
{
    /// <summary>
    /// Provides indexed, locale-aware access to a validated localization catalog.
    /// </summary>
    public sealed partial class I18nRuntime
    {
        private readonly I18nCompiledCatalogStorage _catalog;
        private readonly IReadOnlyDictionary<string, int> _localeIndexes;
        private readonly IReadOnlyList<I18nRuntimeLocale> _locales;
        private readonly int _defaultLocale;
        private int _currentLocale;

        /// <summary>
        /// Initializes a runtime using the catalog default locale.
        /// </summary>
        /// <param name="catalog">The source catalog used to build the runtime snapshot.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is null.</exception>
        /// <exception cref="I18nInvalidCatalogException">Thrown when the catalog contains validation errors.</exception>
        public I18nRuntime(I18nCatalog catalog)
            : this(CompileSourceCatalog(catalog), null, true)
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
            : this(
                CompileSourceCatalog(catalog),
                localeId ?? throw new ArgumentNullException(nameof(localeId)),
                false)
        {
        }

        /// <summary>Initializes a runtime from data prepared by <see cref="I18nCompiledCatalogCompiler"/>.</summary>
        public I18nRuntime(I18nCompiledCatalog catalog)
            : this(catalog, null, true)
        {
        }

        /// <summary>Initializes a runtime from prepared data using the specified locale.</summary>
        public I18nRuntime(I18nCompiledCatalog catalog, string localeId)
            : this(catalog, localeId ?? throw new ArgumentNullException(nameof(localeId)), false)
        {
        }

        private I18nRuntime(I18nCompiledCatalog catalog, string? localeId, bool useDefaultLocale)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            _catalog = catalog.Storage;
            _defaultLocale = _catalog.DefaultLocale;
            _localeIndexes = BuildLocales(_catalog, out _locales);

            int initialLocale;
            if (useDefaultLocale)
            {
                initialLocale = _defaultLocale;
            }
            else if (!_localeIndexes.TryGetValue(localeId!, out initialLocale))
            {
                throw new ArgumentException(
                    $"Locale '{localeId}' is not declared by the catalog.",
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
        public I18nRuntimeLocale DefaultLocale => _locales[_defaultLocale];

        /// <summary>
        /// Gets the currently selected locale.
        /// </summary>
        public I18nRuntimeLocale CurrentLocale => _locales[Volatile.Read(ref _currentLocale)];

        /// <summary>
        /// Gets the culture of the currently selected locale.
        /// </summary>
        public CultureInfo CurrentCulture => CurrentLocale.Culture;

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

            if (!_localeIndexes.TryGetValue(localeId, out int locale))
            {
                throw new ArgumentException(
                    $"Locale '{localeId}' is not declared by the catalog.",
                    nameof(localeId));
            }

            int previous = Interlocked.Exchange(ref _currentLocale, locale);
            if (previous == locale)
            {
                return false;
            }

            LocaleChanged?.Invoke(
                this,
                new I18nLocaleChangedEventArgs(_locales[previous], _locales[locale]));
            return true;
        }

        /// <summary>
        /// Gets localized text, falling back to the entry path or numeric ID when no text exists.
        /// </summary>
        /// <param name="id">The self-identifying stable entry ID.</param>
        /// <returns>The resolved text, entry path, or numeric ID.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="id"/> has an invalid format.</exception>
        public I18nMessageFormatResult Format(long id)
        {
            EnsureValidId(id);

            int message = ResolveMessage(id, out CultureInfo culture);
            if (message != I18nCompiledCatalogFormat.MissingIndex)
            {
                return I18nCompiledMessage.FormatStored(_catalog, message, culture);
            }

            return I18nMessageFormatResult.Success(GetMissingTextFallback(id));
        }

        /// <summary>Formats localized text with one named argument.</summary>
        public I18nMessageFormatResult Format<T1>(long id, (string Name, T1 Value) argument1)
        {
            EnsureValidId(id);
            int message = ResolveMessage(id, out CultureInfo culture);
            return message != I18nCompiledCatalogFormat.MissingIndex
                ? I18nCompiledMessage.FormatStored(_catalog, message, culture, argument1)
                : I18nMessageFormatResult.Success(GetMissingTextFallback(id));
        }

        /// <summary>Formats localized text with two named arguments.</summary>
        public I18nMessageFormatResult Format<T1, T2>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2)
        {
            EnsureValidId(id);
            int message = ResolveMessage(id, out CultureInfo culture);
            return message != I18nCompiledCatalogFormat.MissingIndex
                ? I18nCompiledMessage.FormatStored(_catalog, message, culture, argument1, argument2)
                : I18nMessageFormatResult.Success(GetMissingTextFallback(id));
        }

        /// <summary>Formats localized text with 3 named arguments.</summary>
        public I18nMessageFormatResult Format<T1, T2, T3>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3)
        {
            EnsureValidId(id);
            int message = ResolveMessage(id, out CultureInfo culture);
            return message != I18nCompiledCatalogFormat.MissingIndex
                ? I18nCompiledMessage.FormatStored(_catalog, message, culture, argument1, argument2, argument3)
                : I18nMessageFormatResult.Success(GetMissingTextFallback(id));
        }

        /// <summary>Formats localized text with 4 named arguments.</summary>
        public I18nMessageFormatResult Format<T1, T2, T3, T4>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4)
        {
            EnsureValidId(id);
            int message = ResolveMessage(id, out CultureInfo culture);
            return message != I18nCompiledCatalogFormat.MissingIndex
                ? I18nCompiledMessage.FormatStored(_catalog, message, culture, argument1, argument2, argument3, argument4)
                : I18nMessageFormatResult.Success(GetMissingTextFallback(id));
        }

        /// <summary>Formats localized text with 5 named arguments.</summary>
        public I18nMessageFormatResult Format<T1, T2, T3, T4, T5>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5)
        {
            EnsureValidId(id);
            int message = ResolveMessage(id, out CultureInfo culture);
            return message != I18nCompiledCatalogFormat.MissingIndex
                ? I18nCompiledMessage.FormatStored(_catalog, message, culture, argument1, argument2, argument3, argument4, argument5)
                : I18nMessageFormatResult.Success(GetMissingTextFallback(id));
        }

        /// <summary>Formats localized text with 6 named arguments.</summary>
        public I18nMessageFormatResult Format<T1, T2, T3, T4, T5, T6>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5,
            (string Name, T6 Value) argument6)
        {
            EnsureValidId(id);
            int message = ResolveMessage(id, out CultureInfo culture);
            return message != I18nCompiledCatalogFormat.MissingIndex
                ? I18nCompiledMessage.FormatStored(_catalog, message, culture, argument1, argument2, argument3, argument4, argument5, argument6)
                : I18nMessageFormatResult.Success(GetMissingTextFallback(id));
        }

        /// <summary>Formats localized text with 7 named arguments.</summary>
        public I18nMessageFormatResult Format<T1, T2, T3, T4, T5, T6, T7>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5,
            (string Name, T6 Value) argument6,
            (string Name, T7 Value) argument7)
        {
            EnsureValidId(id);
            int message = ResolveMessage(id, out CultureInfo culture);
            return message != I18nCompiledCatalogFormat.MissingIndex
                ? I18nCompiledMessage.FormatStored(_catalog, message, culture, argument1, argument2, argument3, argument4, argument5, argument6, argument7)
                : I18nMessageFormatResult.Success(GetMissingTextFallback(id));
        }

        /// <summary>Formats localized text with 8 named arguments.</summary>
        public I18nMessageFormatResult Format<T1, T2, T3, T4, T5, T6, T7, T8>(
            long id,
            (string Name, T1 Value) argument1,
            (string Name, T2 Value) argument2,
            (string Name, T3 Value) argument3,
            (string Name, T4 Value) argument4,
            (string Name, T5 Value) argument5,
            (string Name, T6 Value) argument6,
            (string Name, T7 Value) argument7,
            (string Name, T8 Value) argument8)
        {
            EnsureValidId(id);
            int message = ResolveMessage(id, out CultureInfo culture);
            return message != I18nCompiledCatalogFormat.MissingIndex
                ? I18nCompiledMessage.FormatStored(_catalog, message, culture, argument1, argument2, argument3, argument4, argument5, argument6, argument7, argument8)
                : I18nMessageFormatResult.Success(GetMissingTextFallback(id));
        }

        /// <summary>Formats localized text with an uncommon number of named arguments.</summary>
        public I18nMessageFormatResult Format(long id, params (string Name, object? Value)[] arguments)
        {
            EnsureValidId(id);
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            int message = ResolveMessage(id, out CultureInfo culture);
            return message != I18nCompiledCatalogFormat.MissingIndex
                ? I18nCompiledMessage.FormatStored(_catalog, message, culture, arguments)
                : I18nMessageFormatResult.Success(GetMissingTextFallback(id));
        }

        /// <summary>
        /// Attempts to resolve localized text through the current locale fallback chain.
        /// </summary>
        /// <param name="id">The self-identifying stable entry ID.</param>
        /// <param name="text">The resolved text when one exists.</param>
        /// <returns><see langword="true"/> when text was found; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="id"/> has an invalid format.</exception>
        public bool TryGetText(long id, out string? text)
        {
            EnsureValidId(id);
            text = null;

            int message = ResolveMessage(id, out CultureInfo culture);
            if (message == I18nCompiledCatalogFormat.MissingIndex)
            {
                return false;
            }

            text = I18nCompiledMessage.FormatStored(_catalog, message, culture).Text;
            return true;
        }

        private int ResolveMessage(long id, out CultureInfo culture)
        {
            int locale = Volatile.Read(ref _currentLocale);
            culture = _locales[locale].Culture;

            int entryIndex = FindEntry(id);
            if (entryIndex < 0)
            {
                return I18nCompiledCatalogFormat.MissingIndex;
            }

            I18nCompiledLocaleRecord localeRecord = _catalog.Locales[locale];
            for (int index = 0; index < localeRecord.FallbackCount; index++)
            {
                int fallbackLocale = _catalog.FallbackLocales[localeRecord.FirstFallback + index];
                int valueIndex = FindValue(_catalog.Entries[entryIndex], fallbackLocale);
                if (valueIndex >= 0)
                {
                    int message = _catalog.Values[valueIndex].Message;
                    if (message != I18nCompiledCatalogFormat.MissingIndex)
                    {
                        culture = _locales[fallbackLocale].Culture;
                        return message;
                    }
                }
            }

            return I18nCompiledCatalogFormat.MissingIndex;
        }

        private string GetMissingTextFallback(long id)
        {
            int entryIndex = FindEntry(id);
            return entryIndex >= 0
                ? _catalog.GetString(_catalog.Entries[entryIndex].Path)
                : id.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets an asset reference through the current locale fallback chain.
        /// </summary>
        /// <param name="id">The self-identifying stable entry ID.</param>
        /// <returns>The resolved asset reference, or <see langword="null"/> when no asset exists.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="id"/> has an invalid format.</exception>
        public I18nAssetReference? Asset(long id)
        {
            TryGetAsset(id, out I18nAssetReference? asset);
            return asset;
        }

        /// <summary>
        /// Attempts to resolve an asset reference through the current locale fallback chain.
        /// </summary>
        /// <param name="id">The self-identifying stable entry ID.</param>
        /// <param name="asset">The resolved asset reference when one exists.</param>
        /// <returns><see langword="true"/> when an asset was found; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="id"/> has an invalid format.</exception>
        public bool TryGetAsset(long id, out I18nAssetReference? asset)
        {
            EnsureValidId(id);
            asset = null;

            int entryIndex = FindEntry(id);
            if (entryIndex < 0)
            {
                return false;
            }

            int locale = Volatile.Read(ref _currentLocale);
            I18nCompiledLocaleRecord localeRecord = _catalog.Locales[locale];
            for (int index = 0; index < localeRecord.FallbackCount; index++)
            {
                int fallbackLocale = _catalog.FallbackLocales[localeRecord.FirstFallback + index];
                int valueIndex = FindValue(_catalog.Entries[entryIndex], fallbackLocale);
                if (valueIndex >= 0 && _catalog.Values[valueIndex].Asset.HasValue)
                {
                    asset = _catalog.CreateAsset(_catalog.Values[valueIndex].Asset);
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyDictionary<string, int> BuildLocales(
            I18nCompiledCatalogStorage catalog,
            out IReadOnlyList<I18nRuntimeLocale> publicLocales)
        {
            var indexes = new Dictionary<string, int>(catalog.Locales.Length, StringComparer.Ordinal);
            var locales = new I18nRuntimeLocale[catalog.Locales.Length];
            for (int index = 0; index < catalog.Locales.Length; index++)
            {
                I18nCompiledLocaleRecord record = catalog.Locales[index];
                string id = catalog.GetString(record.Id);
                indexes.Add(id, index);
                locales[index] = new I18nRuntimeLocale(catalog, record);
            }

            publicLocales = Array.AsReadOnly(locales);
            return new ReadOnlyDictionary<string, int>(indexes);
        }

        private static I18nCompiledCatalog CompileSourceCatalog(I18nCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            I18nCompiledCatalogCompilation compilation = I18nCompiledCatalogCompiler.Compile(catalog);
            if (compilation.Catalog != null)
            {
                return compilation.Catalog;
            }

            I18nCatalogMessageDiagnostic diagnostic = compilation.Diagnostics[0];
            throw new InvalidDataException(
                $"Entry '{diagnostic.EntryPath}' ({diagnostic.EntryId}), locale " +
                $"'{diagnostic.LocaleId}' contains an invalid message: {diagnostic.Diagnostic.Message}");
        }

        private int FindEntry(long id)
        {
            int low = 0;
            int high = _catalog.Entries.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                long candidate = _catalog.Entries[middle].Id;
                if (candidate == id)
                {
                    return middle;
                }

                if (candidate < id)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return I18nCompiledCatalogFormat.MissingIndex;
        }

        private int FindValue(I18nCompiledEntryRecord entry, int locale)
        {
            int low = entry.FirstValue;
            int high = entry.FirstValue + entry.ValueCount - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int candidate = _catalog.Values[middle].Locale;
                if (candidate == locale)
                {
                    return middle;
                }

                if (candidate < locale)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return I18nCompiledCatalogFormat.MissingIndex;
        }

        private static void EnsureValidId(long id)
        {
            if (!I18nEntryId.IsValid(id))
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "The entry ID format is invalid.");
            }
        }

    }
}
