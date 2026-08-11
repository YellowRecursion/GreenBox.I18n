using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace GreenBox.I18n
{
    /// <summary>
    /// Immutable catalog prepared for runtime use without JSON or MessageFormat parsing.
    /// </summary>
    public sealed class I18nCompiledCatalog
    {
        internal I18nCompiledCatalog(
            string defaultLocaleId,
            List<CompiledLocale> locales,
            List<CompiledEntry> entries)
        {
            DefaultLocaleId = defaultLocaleId;
            Locales = locales.AsReadOnly();
            Entries = entries.AsReadOnly();
        }

        internal string DefaultLocaleId { get; }
        internal IReadOnlyList<CompiledLocale> Locales { get; }
        internal IReadOnlyList<CompiledEntry> Entries { get; }

        internal sealed class CompiledLocale
        {
            public CompiledLocale(
                string id,
                string displayName,
                string cultureName,
                string? fallbackId,
                I18nAssetReference? icon)
            {
                Id = id;
                DisplayName = displayName;
                CultureName = cultureName;
                FallbackId = fallbackId;
                Icon = icon;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public string CultureName { get; }
            public string? FallbackId { get; }
            public I18nAssetReference? Icon { get; }
        }

        internal sealed class CompiledEntry
        {
            public CompiledEntry(long id, string path, List<CompiledValue> values)
            {
                Id = id;
                Path = path;
                Values = values.AsReadOnly();
            }

            public long Id { get; }
            public string Path { get; }
            public IReadOnlyList<CompiledValue> Values { get; }
        }

        internal sealed class CompiledValue
        {
            public CompiledValue(
                string localeId,
                I18nCompiledMessage? message,
                I18nAssetReference? asset)
            {
                LocaleId = localeId;
                Message = message;
                Asset = asset;
            }

            public string LocaleId { get; }
            public I18nCompiledMessage? Message { get; }
            public I18nAssetReference? Asset { get; }
        }
    }

    /// <summary>Describes a source message that could not be compiled.</summary>
    public sealed class I18nCatalogMessageDiagnostic
    {
        internal I18nCatalogMessageDiagnostic(
            long entryId,
            string entryPath,
            string localeId,
            I18nMessageDiagnostic diagnostic)
        {
            EntryId = entryId;
            EntryPath = entryPath;
            LocaleId = localeId;
            Diagnostic = diagnostic;
        }

        /// <summary>Gets the stable entry ID.</summary>
        public long EntryId { get; }
        /// <summary>Gets the human-readable entry path.</summary>
        public string EntryPath { get; }
        /// <summary>Gets the locale containing the invalid message.</summary>
        public string LocaleId { get; }
        /// <summary>Gets the underlying MessageFormat diagnostic.</summary>
        public I18nMessageDiagnostic Diagnostic { get; }
    }

    /// <summary>Result of preparing a source catalog for runtime use.</summary>
    public sealed class I18nCompiledCatalogCompilation
    {
        private readonly ReadOnlyCollection<I18nCatalogMessageDiagnostic> _diagnostics;

        internal I18nCompiledCatalogCompilation(
            I18nCompiledCatalog? catalog,
            List<I18nCatalogMessageDiagnostic> diagnostics)
        {
            Catalog = catalog;
            _diagnostics = diagnostics.AsReadOnly();
        }

        /// <summary>Gets the prepared catalog, or null when compilation failed.</summary>
        public I18nCompiledCatalog? Catalog { get; }
        /// <summary>Gets source message diagnostics in deterministic order.</summary>
        public IReadOnlyList<I18nCatalogMessageDiagnostic> Diagnostics => _diagnostics;
        /// <summary>Gets whether the complete catalog was compiled successfully.</summary>
        public bool IsSuccess => Catalog != null;
    }

    /// <summary>Compiles validated source catalogs into runtime catalogs.</summary>
    public static class I18nCompiledCatalogCompiler
    {
        /// <summary>Validates and compiles the complete source catalog.</summary>
        public static I18nCompiledCatalogCompilation Compile(I18nCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            I18nValidationResult validation = I18nCatalogValidator.Validate(catalog);
            if (validation.HasErrors)
            {
                throw new I18nInvalidCatalogException(validation);
            }

            var diagnostics = new List<I18nCatalogMessageDiagnostic>();
            var locales = new List<I18nCompiledCatalog.CompiledLocale>(catalog.Locales.Count);
            for (int index = 0; index < catalog.Locales.Count; index++)
            {
                I18nLocaleDefinition locale = catalog.Locales[index];
                locales.Add(new I18nCompiledCatalog.CompiledLocale(
                    locale.Id,
                    locale.DisplayName,
                    locale.Culture,
                    locale.Fallback,
                    CloneAsset(locale.Icon)));
            }

            var entries = new List<I18nCompiledCatalog.CompiledEntry>(catalog.Entries.Count);
            for (int entryIndex = 0; entryIndex < catalog.Entries.Count; entryIndex++)
            {
                I18nEntry entry = catalog.Entries[entryIndex];
                long entryId = long.Parse(entry.Id, NumberStyles.None, CultureInfo.InvariantCulture);
                var values = new List<I18nCompiledCatalog.CompiledValue>(entry.Locales.Count);
                HashSet<string>? expectedArguments = null;

                var localeIds = new List<string>(entry.Locales.Keys);
                localeIds.Sort(StringComparer.Ordinal);
                for (int localeIndex = 0; localeIndex < localeIds.Count; localeIndex++)
                {
                    string localeId = localeIds[localeIndex];
                    I18nLocaleValue value = entry.Locales[localeId];
                    I18nCompiledMessage? message = null;
                    if (value.Text != null)
                    {
                        I18nMessageCompilation messageCompilation =
                            I18nMessageCompiler.Compile(value.Text);
                        if (!messageCompilation.IsSuccess)
                        {
                            for (int diagnosticIndex = 0;
                                 diagnosticIndex < messageCompilation.Diagnostics.Count;
                                 diagnosticIndex++)
                            {
                                diagnostics.Add(new I18nCatalogMessageDiagnostic(
                                    entryId,
                                    entry.Path,
                                    localeId,
                                    messageCompilation.Diagnostics[diagnosticIndex]));
                            }
                        }
                        else
                        {
                            message = messageCompilation.Message!;
                            var arguments = new HashSet<string>(message.ArgumentNames, StringComparer.Ordinal);
                            if (expectedArguments == null)
                            {
                                expectedArguments = arguments;
                            }
                            else if (!expectedArguments.SetEquals(arguments))
                            {
                                diagnostics.Add(new I18nCatalogMessageDiagnostic(
                                    entryId,
                                    entry.Path,
                                    localeId,
                                    new I18nMessageDiagnostic(
                                        "inconsistent_arguments",
                                        "All populated locales of an entry must use the same argument names.",
                                        0,
                                        string.Empty)));
                            }
                        }
                    }

                    values.Add(new I18nCompiledCatalog.CompiledValue(
                        localeId,
                        message,
                        CloneAsset(value.Asset)));
                }

                entries.Add(new I18nCompiledCatalog.CompiledEntry(entryId, entry.Path, values));
            }

            return diagnostics.Count == 0
                ? new I18nCompiledCatalogCompilation(
                    new I18nCompiledCatalog(catalog.DefaultLocale, locales, entries), diagnostics)
                : new I18nCompiledCatalogCompilation(null, diagnostics);
        }

        private static I18nAssetReference? CloneAsset(I18nAssetReference? source)
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
