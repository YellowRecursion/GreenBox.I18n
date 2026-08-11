#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GreenBox.I18n.Unity.Editor.Compilation;
using GreenBox.I18n.Unity.Editor.Settings;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor
{
    /// <summary>
    /// Compiles engine-independent catalog asset references into serialized Unity objects.
    /// </summary>
    public static class I18nCatalogCompiler
    {
        /// <summary>
        /// Gets the current compilation state without modifying the catalog asset.
        /// </summary>
        /// <param name="catalogAsset">Catalog asset to inspect.</param>
        /// <returns>The state of its generated runtime data.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalogAsset"/> is null.</exception>
        public static I18nCatalogCompilationState GetState(I18nCatalogAsset catalogAsset)
        {
            if (catalogAsset == null)
            {
                throw new ArgumentNullException(nameof(catalogAsset));
            }

            TextAsset? sourceCatalog = I18nProjectSettings.instance.SourceCatalog;
            if (!sourceCatalog ||
                !catalogAsset.HasCompiledCatalog ||
                string.IsNullOrEmpty(catalogAsset.SourceHash))
            {
                return I18nCatalogCompilationState.NotCompiled;
            }

            string sourceHash = ComputeSourceHash(sourceCatalog.bytes);
            if (!string.Equals(sourceHash, catalogAsset.SourceHash, StringComparison.Ordinal))
            {
                return I18nCatalogCompilationState.OutOfDate;
            }

            for (int bindingIndex = 0; bindingIndex < catalogAsset.AssetBindings.Count; bindingIndex++)
            {
                if (!catalogAsset.AssetBindings[bindingIndex].Asset)
                {
                    return I18nCatalogCompilationState.OutOfDate;
                }
            }

            return I18nCatalogCompilationState.UpToDate;
        }

        /// <summary>
        /// Validates and compiles a catalog asset without modifying it when any error occurs.
        /// </summary>
        /// <param name="catalogAsset">The Unity catalog asset to compile.</param>
        /// <returns>The complete compilation result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalogAsset"/> is null.</exception>
        public static I18nCatalogCompilationResult Compile(I18nCatalogAsset catalogAsset)
        {
            if (catalogAsset == null)
            {
                throw new ArgumentNullException(nameof(catalogAsset));
            }

            var errors = new List<I18nCatalogCompilationError>();
            TextAsset? sourceCatalog = I18nProjectSettings.instance.SourceCatalog;
            if (!sourceCatalog)
            {
                errors.Add(new I18nCatalogCompilationError(
                    I18nCatalogCompilationCodes.MissingSourceCatalog,
                    "$",
                    "The project source localization.json could not be found."));
                return Finish(
                    catalogAsset,
                    new I18nCatalogCompilationResult(null, errors, false, 0));
            }

            I18nCatalog catalog;
            try
            {
                catalog = I18nCatalogJson.Deserialize(sourceCatalog.text);
            }
            catch (JsonException exception)
            {
                errors.Add(new I18nCatalogCompilationError(
                    I18nCatalogCompilationCodes.InvalidJson,
                    "$",
                    exception.Message));
                return Finish(
                    catalogAsset,
                    new I18nCatalogCompilationResult(null, errors, false, 0));
            }

            I18nValidationResult validationResult = I18nCatalogValidator.Validate(catalog);
            if (validationResult.HasErrors)
            {
                return Finish(
                    catalogAsset,
                    new I18nCatalogCompilationResult(validationResult, errors, false, 0));
            }


            I18nCompiledCatalogCompilation runtimeCompilation =
                I18nCompiledCatalogCompiler.Compile(catalog);
            if (!runtimeCompilation.IsSuccess)
            {
                for (int diagnosticIndex = 0;
                     diagnosticIndex < runtimeCompilation.Diagnostics.Count;
                     diagnosticIndex++)
                {
                    I18nCatalogMessageDiagnostic diagnostic =
                        runtimeCompilation.Diagnostics[diagnosticIndex];
                    errors.Add(new I18nCatalogCompilationError(
                        I18nCatalogCompilationCodes.InvalidMessage,
                        $"entry:{diagnostic.EntryId}/locale:{diagnostic.LocaleId}",
                        diagnostic.Diagnostic.Message));
                }

                return Finish(
                    catalogAsset,
                    new I18nCatalogCompilationResult(validationResult, errors, false, 0));
            }

            byte[] compiledCatalog =
                I18nCompiledCatalogBinary.Serialize(runtimeCompilation.Catalog!);

            List<SourceAssetReference> sourceReferences = CollectAssetReferences(catalog);
            List<I18nAssetBinding> bindings = ResolveAssetBindings(sourceReferences, errors);
            if (errors.Count > 0)
            {
                return Finish(
                    catalogAsset,
                    new I18nCatalogCompilationResult(validationResult, errors, false, 0));
            }

            string sourceHash = ComputeSourceHash(sourceCatalog.bytes);
            bool hasChanges =
                !string.Equals(sourceHash, catalogAsset.SourceHash, StringComparison.Ordinal) ||
                !catalogAsset.HasCompiledCatalog ||
                !BindingsEqual(catalogAsset.AssetBindings, bindings);

            if (hasChanges)
            {
                catalogAsset.ReplaceCompiledData(sourceHash, compiledCatalog, bindings);
                EditorUtility.SetDirty(catalogAsset);
                AssetDatabase.SaveAssetIfDirty(catalogAsset);
            }

            return Finish(
                catalogAsset,
                new I18nCatalogCompilationResult(
                    validationResult,
                    errors,
                    hasChanges,
                    bindings.Count));
        }

        private static I18nCatalogCompilationResult Finish(
            I18nCatalogAsset catalogAsset,
            I18nCatalogCompilationResult result)
        {
            I18nCatalogCompilationEvents.Publish(catalogAsset, result);
            return result;
        }

        private static List<SourceAssetReference> CollectAssetReferences(I18nCatalog catalog)
        {
            var uniqueReferences = new Dictionary<AssetReferenceKey, SourceAssetReference>();

            for (int localeIndex = 0; localeIndex < catalog.Locales.Count; localeIndex++)
            {
                I18nAssetReference? icon = catalog.Locales[localeIndex].Icon;
                AddAssetReference(
                    uniqueReferences,
                    icon,
                    $"$.locales[{localeIndex}].icon");
            }

            for (int entryIndex = 0; entryIndex < catalog.Entries.Count; entryIndex++)
            {
                I18nEntry entry = catalog.Entries[entryIndex];
                var localeIds = new List<string>(entry.Locales.Keys);
                localeIds.Sort(StringComparer.Ordinal);

                for (int localeIndex = 0; localeIndex < localeIds.Count; localeIndex++)
                {
                    string localeId = localeIds[localeIndex];
                    AddAssetReference(
                        uniqueReferences,
                        entry.Locales[localeId].Asset,
                        $"$.entries[{entryIndex}].locales['{localeId}'].asset");
                }
            }

            var result = new List<SourceAssetReference>(uniqueReferences.Values);
            result.Sort(SourceAssetReferenceComparer.Instance);
            return result;
        }

        private static void AddAssetReference(
            Dictionary<AssetReferenceKey, SourceAssetReference> uniqueReferences,
            I18nAssetReference? assetReference,
            string jsonPath)
        {
            if (assetReference == null)
            {
                return;
            }

            string localFileId = assetReference.LocalFileId ?? string.Empty;
            var key = new AssetReferenceKey(assetReference.AssetGuid, localFileId);
            if (!uniqueReferences.ContainsKey(key))
            {
                uniqueReferences.Add(
                    key,
                    new SourceAssetReference(
                        assetReference.AssetGuid,
                        localFileId,
                        jsonPath));
            }
        }

        private static List<I18nAssetBinding> ResolveAssetBindings(
            IReadOnlyList<SourceAssetReference> sourceReferences,
            List<I18nCatalogCompilationError> errors)
        {
            var bindings = new List<I18nAssetBinding>(sourceReferences.Count);

            for (int referenceIndex = 0; referenceIndex < sourceReferences.Count; referenceIndex++)
            {
                SourceAssetReference sourceReference = sourceReferences[referenceIndex];
                string assetPath = AssetDatabase.GUIDToAssetPath(sourceReference.AssetGuid);

                if (string.IsNullOrEmpty(assetPath))
                {
                    errors.Add(new I18nCatalogCompilationError(
                        I18nCatalogCompilationCodes.AssetNotFound,
                        sourceReference.JsonPath,
                        $"Unity asset GUID '{sourceReference.AssetGuid}' cannot be resolved."));
                    continue;
                }

                UnityEngine.Object? asset = ResolveUnityObject(assetPath, sourceReference.LocalFileId);
                if (!asset)
                {
                    string code = sourceReference.LocalFileId.Length == 0
                        ? I18nCatalogCompilationCodes.AssetNotFound
                        : I18nCatalogCompilationCodes.AssetSubObjectNotFound;
                    string identifier = sourceReference.LocalFileId.Length == 0
                        ? $"GUID '{sourceReference.AssetGuid}'"
                        : $"GUID '{sourceReference.AssetGuid}' and local file ID '{sourceReference.LocalFileId}'";

                    errors.Add(new I18nCatalogCompilationError(
                        code,
                        sourceReference.JsonPath,
                        $"Unity asset with {identifier} cannot be loaded."));
                    continue;
                }

                bindings.Add(new I18nAssetBinding(
                    sourceReference.AssetGuid,
                    sourceReference.LocalFileId,
                    asset));
            }

            return bindings;
        }

        private static UnityEngine.Object? ResolveUnityObject(string assetPath, string localFileId)
        {
            if (localFileId.Length == 0)
            {
                return AssetDatabase.LoadMainAssetAtPath(assetPath);
            }

            long expectedLocalFileId = long.Parse(
                localFileId,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

            for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
            {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        assets[assetIndex],
                        out _,
                        out long actualLocalFileId) &&
                    actualLocalFileId == expectedLocalFileId)
                {
                    return assets[assetIndex];
                }
            }

            return null;
        }

        private static string ComputeSourceHash(byte[] sourceBytes)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(sourceBytes);
            var result = new StringBuilder(hash.Length * 2);

            for (int byteIndex = 0; byteIndex < hash.Length; byteIndex++)
            {
                result.Append(hash[byteIndex].ToString("x2", CultureInfo.InvariantCulture));
            }

            return "v1:" + result;
        }

        private static bool BindingsEqual(
            IReadOnlyList<I18nAssetBinding> left,
            IReadOnlyList<I18nAssetBinding> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (int bindingIndex = 0; bindingIndex < left.Count; bindingIndex++)
            {
                I18nAssetBinding leftBinding = left[bindingIndex];
                I18nAssetBinding rightBinding = right[bindingIndex];

                if (!string.Equals(leftBinding.AssetGuid, rightBinding.AssetGuid, StringComparison.Ordinal) ||
                    !string.Equals(leftBinding.LocalFileId, rightBinding.LocalFileId, StringComparison.Ordinal) ||
                    leftBinding.Asset != rightBinding.Asset)
                {
                    return false;
                }
            }

            return true;
        }

        private readonly struct AssetReferenceKey : IEquatable<AssetReferenceKey>
        {
            public AssetReferenceKey(string assetGuid, string localFileId)
            {
                AssetGuid = assetGuid;
                LocalFileId = localFileId;
            }

            public string AssetGuid { get; }

            public string LocalFileId { get; }

            public bool Equals(AssetReferenceKey other)
            {
                return
                    string.Equals(AssetGuid, other.AssetGuid, StringComparison.Ordinal) &&
                    string.Equals(LocalFileId, other.LocalFileId, StringComparison.Ordinal);
            }

            public override bool Equals(object? obj)
            {
                return obj is AssetReferenceKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return
                        (StringComparer.Ordinal.GetHashCode(AssetGuid) * 397) ^
                        StringComparer.Ordinal.GetHashCode(LocalFileId);
                }
            }
        }

        private readonly struct SourceAssetReference
        {
            public SourceAssetReference(string assetGuid, string localFileId, string jsonPath)
            {
                AssetGuid = assetGuid;
                LocalFileId = localFileId;
                JsonPath = jsonPath;
            }

            public string AssetGuid { get; }

            public string LocalFileId { get; }

            public string JsonPath { get; }
        }

        private sealed class SourceAssetReferenceComparer : IComparer<SourceAssetReference>
        {
            public static readonly SourceAssetReferenceComparer Instance = new();

            public int Compare(SourceAssetReference left, SourceAssetReference right)
            {
                int guidComparison = string.Compare(
                    left.AssetGuid,
                    right.AssetGuid,
                    StringComparison.Ordinal);
                if (guidComparison != 0)
                {
                    return guidComparison;
                }

                return string.Compare(
                    left.LocalFileId,
                    right.LocalFileId,
                    StringComparison.Ordinal);
            }
        }
    }
}
