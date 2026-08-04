#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GreenBox.I18n.Unity.Editor
{
    /// <summary>
    /// Describes whether a Unity catalog asset matches its current JSON source.
    /// </summary>
    public enum I18nCatalogCompilationState
    {
        /// <summary>
        /// The catalog has never been compiled or has no source JSON.
        /// </summary>
        NotCompiled,

        /// <summary>
        /// The source or a compiled Unity object changed after compilation.
        /// </summary>
        OutOfDate,

        /// <summary>
        /// The compiled data matches the current source.
        /// </summary>
        UpToDate,
    }

    /// <summary>
    /// Contains stable machine-readable codes produced by Unity catalog compilation.
    /// </summary>
    public static class I18nCatalogCompilationCodes
    {
        /// <summary>Indicates that no source JSON asset is assigned.</summary>
        public const string MissingSourceCatalog = "missing_source_catalog";

        /// <summary>Indicates that the source JSON cannot be deserialized.</summary>
        public const string InvalidJson = "invalid_json";

        /// <summary>Indicates that a Unity asset GUID cannot be resolved.</summary>
        public const string AssetNotFound = "asset_not_found";

        /// <summary>Indicates that a Unity sub-asset local file identifier cannot be resolved.</summary>
        public const string AssetSubObjectNotFound = "asset_sub_object_not_found";
    }

    /// <summary>
    /// Describes a Unity-specific catalog compilation failure.
    /// </summary>
    public readonly struct I18nCatalogCompilationError
    {
        /// <summary>
        /// Initializes a compilation error.
        /// </summary>
        public I18nCatalogCompilationError(string code, string jsonPath, string message)
        {
            Code = code ?? string.Empty;
            JsonPath = jsonPath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        /// <summary>Gets the stable error code.</summary>
        public string Code { get; }

        /// <summary>Gets the JSON path of the value that caused the error.</summary>
        public string JsonPath { get; }

        /// <summary>Gets the human-readable error message.</summary>
        public string Message { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Code} at {JsonPath}: {Message}";
        }
    }

    /// <summary>
    /// Contains the immutable result of compiling an <see cref="I18nCatalogAsset"/>.
    /// </summary>
    public sealed class I18nCatalogCompilationResult
    {
        private readonly ReadOnlyCollection<I18nCatalogCompilationError> _errors;

        internal I18nCatalogCompilationResult(
            I18nValidationResult? validationResult,
            List<I18nCatalogCompilationError> errors,
            bool hasChanges,
            int assetBindingCount)
        {
            ValidationResult = validationResult;
            _errors = new List<I18nCatalogCompilationError>(errors).AsReadOnly();
            HasChanges = hasChanges;
            AssetBindingCount = assetBindingCount;
        }

        /// <summary>
        /// Gets the source validation result, or <see langword="null"/> when validation could not run.
        /// </summary>
        public I18nValidationResult? ValidationResult { get; }

        /// <summary>
        /// Gets Unity-specific compilation errors in deterministic order.
        /// </summary>
        public IReadOnlyList<I18nCatalogCompilationError> Errors => _errors;

        /// <summary>
        /// Gets a value indicating whether compilation completed successfully.
        /// </summary>
        public bool IsSuccess =>
            _errors.Count == 0 &&
            ValidationResult != null &&
            ValidationResult.IsValid;

        /// <summary>
        /// Gets a value indicating whether the catalog asset was modified.
        /// </summary>
        public bool HasChanges { get; }

        /// <summary>
        /// Gets the number of unique Unity asset bindings produced on success.
        /// </summary>
        public int AssetBindingCount { get; }
    }
}
