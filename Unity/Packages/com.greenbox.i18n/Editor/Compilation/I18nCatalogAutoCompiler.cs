#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using GreenBox.I18n.Unity.Editor.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Compilation
{
    /// <summary>
    /// Recompiles Unity catalogs after their JSON source assets are imported.
    /// </summary>
    internal sealed class I18nCatalogAutoCompiler : AssetPostprocessor
    {
        private static readonly HashSet<string> PendingCatalogGuids = new(StringComparer.Ordinal);
        private static bool _isScheduled;

        /// <summary>
        /// Restores catalog compilation after a script reload clears an import-time pending queue.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void QueueOutOfDateCatalogsAfterReload()
        {
            EditorApplication.delayCall += QueueAllOutOfDateCatalogs;
        }

        /// <summary>
        /// Queues a catalog for compilation outside the current import or Inspector callback.
        /// </summary>
        internal static void Queue(I18nCatalogAsset catalogAsset)
        {
            string assetPath = AssetDatabase.GetAssetPath(catalogAsset);
            string assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(assetGuid))
            {
                return;
            }

            PendingCatalogGuids.Add(assetGuid);
            if (_isScheduled)
            {
                return;
            }

            _isScheduled = true;
            EditorApplication.delayCall += CompilePendingCatalogs;
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var importedJsonPaths = new HashSet<string>(StringComparer.Ordinal);

            for (int assetIndex = 0; assetIndex < importedAssets.Length; assetIndex++)
            {
                string assetPath = importedAssets[assetIndex];
                if (assetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    importedJsonPaths.Add(assetPath);
                }

                if (!assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                I18nCatalogAsset? importedCatalog =
                    AssetDatabase.LoadAssetAtPath<I18nCatalogAsset>(assetPath);
                if (importedCatalog &&
                    importedCatalog.SourceCatalog &&
                    I18nCatalogCompiler.GetState(importedCatalog) !=
                    I18nCatalogCompilationState.UpToDate)
                {
                    Queue(importedCatalog);
                }
            }

            if (importedJsonPaths.Count == 0)
            {
                return;
            }

            string[] catalogGuids = AssetDatabase.FindAssets("t:I18nCatalogAsset");
            for (int catalogIndex = 0; catalogIndex < catalogGuids.Length; catalogIndex++)
            {
                string catalogPath = AssetDatabase.GUIDToAssetPath(catalogGuids[catalogIndex]);
                I18nCatalogAsset? catalogAsset =
                    AssetDatabase.LoadAssetAtPath<I18nCatalogAsset>(catalogPath);
                if (!catalogAsset || !catalogAsset.SourceCatalog)
                {
                    continue;
                }

                string sourcePath = AssetDatabase.GetAssetPath(catalogAsset.SourceCatalog);
                if (importedJsonPaths.Contains(sourcePath))
                {
                    Queue(catalogAsset);
                }
            }
        }

        private static void QueueAllOutOfDateCatalogs()
        {
            string[] catalogGuids = AssetDatabase.FindAssets("t:I18nCatalogAsset");
            for (int catalogIndex = 0; catalogIndex < catalogGuids.Length; catalogIndex++)
            {
                string catalogPath = AssetDatabase.GUIDToAssetPath(catalogGuids[catalogIndex]);
                I18nCatalogAsset? catalogAsset =
                    AssetDatabase.LoadAssetAtPath<I18nCatalogAsset>(catalogPath);
                if (catalogAsset &&
                    catalogAsset.SourceCatalog &&
                    I18nCatalogCompiler.GetState(catalogAsset) !=
                    I18nCatalogCompilationState.UpToDate)
                {
                    Queue(catalogAsset);
                }
            }
        }

        private static void CompilePendingCatalogs()
        {
            _isScheduled = false;
            string[] catalogGuids = new string[PendingCatalogGuids.Count];
            PendingCatalogGuids.CopyTo(catalogGuids);
            PendingCatalogGuids.Clear();
            Array.Sort(catalogGuids, StringComparer.Ordinal);

            for (int catalogIndex = 0; catalogIndex < catalogGuids.Length; catalogIndex++)
            {
                string catalogPath = AssetDatabase.GUIDToAssetPath(catalogGuids[catalogIndex]);
                I18nCatalogAsset? catalogAsset =
                    AssetDatabase.LoadAssetAtPath<I18nCatalogAsset>(catalogPath);
                if (!catalogAsset)
                {
                    continue;
                }

                I18nCatalogCompilationResult result = I18nCatalogCompiler.Compile(catalogAsset);
                if (!result.IsSuccess)
                {
                    I18nLog.Error(BuildFailureMessage(catalogAsset, result), catalogAsset);
                }
            }
        }

        private static string BuildFailureMessage(
            I18nCatalogAsset catalogAsset,
            I18nCatalogCompilationResult result)
        {
            var message = new StringBuilder();
            message.Append("Failed to compile localization catalog '");
            message.Append(catalogAsset.name);
            message.Append("'.");

            if (result.ValidationResult != null)
            {
                for (int diagnosticIndex = 0;
                     diagnosticIndex < result.ValidationResult.Diagnostics.Count;
                     diagnosticIndex++)
                {
                    message.AppendLine();
                    message.Append(result.ValidationResult.Diagnostics[diagnosticIndex]);
                }
            }

            for (int errorIndex = 0; errorIndex < result.Errors.Count; errorIndex++)
            {
                message.AppendLine();
                message.Append(result.Errors[errorIndex]);
            }

            return message.ToString();
        }
    }
}
