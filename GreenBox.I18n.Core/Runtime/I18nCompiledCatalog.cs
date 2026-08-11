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
        internal I18nCompiledCatalog(I18nCompiledCatalogStorage storage)
        {
            Storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        internal I18nCompiledCatalogStorage Storage { get; }
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
            var builder = new I18nCompiledCatalogBuilder(catalog.Locales, catalog.DefaultLocale);
            for (int entryIndex = 0; entryIndex < catalog.Entries.Count; entryIndex++)
            {
                I18nEntry entry = catalog.Entries[entryIndex];
                long entryId = long.Parse(entry.Id, NumberStyles.None, CultureInfo.InvariantCulture);
                var values = new List<I18nCompiledCatalogBuilder.PendingValue>(entry.Locales.Count);
                Dictionary<string, I18nCompiledMessage.I18nMessageArgumentKind>? expectedArguments = null;

                var localeIds = new List<string>(entry.Locales.Keys);
                localeIds.Sort(StringComparer.Ordinal);
                for (int localeIndex = 0; localeIndex < localeIds.Count; localeIndex++)
                {
                    string localeId = localeIds[localeIndex];
                    I18nLocaleValue value = entry.Locales[localeId];
                    int message = I18nCompiledCatalogFormat.MissingIndex;
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
                            I18nCompiledMessage compiledMessage = messageCompilation.Message!;
                            message = builder.AddMessage(compiledMessage);
                            Dictionary<string, I18nCompiledMessage.I18nMessageArgumentKind> arguments =
                                BuildArgumentContract(compiledMessage);
                            if (expectedArguments == null)
                            {
                                expectedArguments = arguments;
                            }
                            else if (!ArgumentContractsEqual(expectedArguments, arguments))
                            {
                                diagnostics.Add(new I18nCatalogMessageDiagnostic(
                                    entryId,
                                    entry.Path,
                                    localeId,
                                    new I18nMessageDiagnostic(
                                        "inconsistent_arguments",
                                        "All populated locales of an entry must use the same argument names and types.",
                                        0,
                                        string.Empty)));
                            }
                        }
                    }

                    values.Add(builder.CreateValue(
                        localeId,
                        message,
                        value.Asset));
                }

                builder.AddEntry(entryId, entry.Path, values);
            }

            return diagnostics.Count == 0
                ? new I18nCompiledCatalogCompilation(
                    builder.Build(), diagnostics)
                : new I18nCompiledCatalogCompilation(null, diagnostics);
        }

        private static Dictionary<string, I18nCompiledMessage.I18nMessageArgumentKind>
            BuildArgumentContract(I18nCompiledMessage message)
        {
            var result = new Dictionary<string, I18nCompiledMessage.I18nMessageArgumentKind>(
                message.ArgumentNames.Count,
                StringComparer.Ordinal);
            for (int index = 0; index < message.ArgumentNames.Count; index++)
            {
                result.Add(message.ArgumentNames[index], message.ArgumentKinds[index]);
            }

            return result;
        }

        private static bool ArgumentContractsEqual(
            Dictionary<string, I18nCompiledMessage.I18nMessageArgumentKind> left,
            Dictionary<string, I18nCompiledMessage.I18nMessageArgumentKind> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, I18nCompiledMessage.I18nMessageArgumentKind> pair in left)
            {
                if (!right.TryGetValue(pair.Key, out I18nCompiledMessage.I18nMessageArgumentKind kind) ||
                    kind != pair.Value)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
