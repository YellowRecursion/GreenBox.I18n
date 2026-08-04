using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GreenBox.I18n
{
    /// <summary>
    /// Defines the severity of an i18n validation diagnostic.
    /// </summary>
    public enum I18nValidationSeverity
    {
        /// <summary>
        /// Identifies a non-blocking problem.
        /// </summary>
        Warning,

        /// <summary>
        /// Identifies a problem that makes the validated data invalid.
        /// </summary>
        Error,
    }

    /// <summary>
    /// Contains stable machine-readable codes produced by i18n validation rules.
    /// </summary>
    public static class I18nValidationCodes
    {
        /// <summary>Indicates that the catalog is null.</summary>
        public const string NullCatalog = "null_catalog";

        /// <summary>Indicates that the schema version is not supported.</summary>
        public const string UnsupportedSchemaVersion = "unsupported_schema_version";

        /// <summary>Indicates that the entries collection is null.</summary>
        public const string NullEntries = "null_entries";

        /// <summary>Indicates that an entry is null.</summary>
        public const string NullEntry = "null_entry";

        /// <summary>Indicates that an entry ID is missing.</summary>
        public const string MissingId = "missing_id";

        /// <summary>Indicates that an entry ID has an invalid format or value.</summary>
        public const string InvalidId = "invalid_id";

        /// <summary>Indicates that an entry ID is used more than once.</summary>
        public const string DuplicateId = "duplicate_id";

        /// <summary>Indicates that an entry path is missing.</summary>
        public const string MissingPath = "missing_path";

        /// <summary>Indicates that an entry path has an invalid format.</summary>
        public const string InvalidPath = "invalid_path";

        /// <summary>Indicates that an entry path is used more than once.</summary>
        public const string DuplicatePath = "duplicate_path";

        /// <summary>Indicates that an entry has no locale values.</summary>
        public const string MissingLocales = "missing_locales";

        /// <summary>Indicates that a locale identifier is invalid.</summary>
        public const string InvalidLocaleId = "invalid_locale_id";

        /// <summary>Indicates that a locale value is null.</summary>
        public const string NullLocaleValue = "null_locale_value";

        /// <summary>Indicates that a locale value contains neither text nor an asset.</summary>
        public const string EmptyLocaleValue = "empty_locale_value";

        /// <summary>Indicates that an asset reference has no GUID.</summary>
        public const string MissingAssetGuid = "missing_asset_guid";

        /// <summary>Indicates that an asset GUID has an invalid format.</summary>
        public const string InvalidAssetGuid = "invalid_asset_guid";

        /// <summary>Indicates that an asset local file identifier has an invalid format.</summary>
        public const string InvalidAssetLocalFileId = "invalid_asset_local_file_id";
    }

    /// <summary>
    /// Describes a single problem found while validating i18n source data.
    /// </summary>
    public readonly struct I18nValidationDiagnostic
    {
        /// <summary>
        /// Initializes a new validation diagnostic.
        /// </summary>
        /// <param name="code">The validation rule that produced the diagnostic.</param>
        /// <param name="severity">The severity of the problem.</param>
        /// <param name="jsonPath">The JSON path of the invalid value.</param>
        /// <param name="message">A human-readable description of the problem.</param>
        public I18nValidationDiagnostic(
            string code,
            I18nValidationSeverity severity,
            string jsonPath,
            string message)
        {
            Code = code ?? string.Empty;
            Severity = severity;
            JsonPath = jsonPath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        /// <summary>
        /// Gets the validation rule that produced this diagnostic.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the severity of the problem.
        /// </summary>
        public I18nValidationSeverity Severity { get; }

        /// <summary>
        /// Gets the JSON path of the invalid value.
        /// </summary>
        public string JsonPath { get; }

        /// <summary>
        /// Gets the human-readable description of the problem.
        /// </summary>
        public string Message { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Severity} {Code} at {JsonPath}: {Message}";
        }
    }

    /// <summary>
    /// Contains the complete immutable result of an i18n validation operation.
    /// </summary>
    public sealed class I18nValidationResult
    {
        private readonly ReadOnlyCollection<I18nValidationDiagnostic> _diagnostics;

        internal I18nValidationResult(List<I18nValidationDiagnostic> diagnostics)
        {
            _diagnostics = new List<I18nValidationDiagnostic>(diagnostics).AsReadOnly();

            for (int i = 0; i < _diagnostics.Count; i++)
            {
                if (_diagnostics[i].Severity == I18nValidationSeverity.Error)
                {
                    ErrorCount++;
                }
                else
                {
                    WarningCount++;
                }
            }
        }

        /// <summary>
        /// Gets all diagnostics in deterministic validation order.
        /// </summary>
        public IReadOnlyList<I18nValidationDiagnostic> Diagnostics => _diagnostics;

        /// <summary>
        /// Gets the number of error diagnostics.
        /// </summary>
        public int ErrorCount { get; }

        /// <summary>
        /// Gets the number of warning diagnostics.
        /// </summary>
        public int WarningCount { get; }

        /// <summary>
        /// Gets a value indicating whether validation produced at least one error.
        /// </summary>
        public bool HasErrors => ErrorCount > 0;

        /// <summary>
        /// Gets a value indicating whether validation produced at least one warning.
        /// </summary>
        public bool HasWarnings => WarningCount > 0;

        /// <summary>
        /// Gets a value indicating whether validation completed without errors.
        /// </summary>
        public bool IsValid => !HasErrors;

        /// <summary>
        /// Determines whether the result contains a diagnostic with the specified code.
        /// </summary>
        /// <param name="code">The diagnostic code to find.</param>
        /// <returns><see langword="true"/> when a matching diagnostic exists.</returns>
        public bool Contains(string code)
        {
            for (int i = 0; i < _diagnostics.Count; i++)
            {
                if (string.Equals(_diagnostics[i].Code, code, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
