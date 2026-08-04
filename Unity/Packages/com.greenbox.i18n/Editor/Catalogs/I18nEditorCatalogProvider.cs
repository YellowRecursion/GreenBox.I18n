#nullable enable

using System;
using UnityEditor;
using UnityEngine;
using GreenBox.I18n.Unity.Editor.Settings;

namespace GreenBox.I18n.Unity.Editor.Catalogs
{
    /// <summary>
    /// Loads and caches the source catalog used by Unity editor tools.
    /// </summary>
    internal static class I18nEditorCatalogProvider
    {
        private static int _catalogInstanceId;
        private static int _sourceInstanceId;
        private static Hash128 _sourceHash;
        private static bool _isInitialized;
        private static I18nCatalog? _catalog;
        private static string? _error;

        /// <summary>
        /// Gets the current source catalog, rebuilding the cache after its imported contents change.
        /// </summary>
        /// <param name="error">Receives an editor-facing error when the catalog is unavailable.</param>
        /// <returns>The current catalog, or <see langword="null"/> when it cannot be loaded.</returns>
        internal static I18nCatalog? GetCatalog(out string? error)
        {
            I18nCatalogAsset? catalogAsset = I18nProjectSettings.instance.ActiveCatalog;
            TextAsset? source = catalogAsset ? catalogAsset.SourceCatalog : null;
            Hash128 sourceHash = GetDependencyHash(source);
            int catalogInstanceId = catalogAsset ? catalogAsset.GetInstanceID() : 0;
            int sourceInstanceId = source ? source.GetInstanceID() : 0;

            if (!_isInitialized ||
                _catalogInstanceId != catalogInstanceId ||
                _sourceInstanceId != sourceInstanceId ||
                _sourceHash != sourceHash)
            {
                Reload(catalogAsset, source, sourceHash);
            }

            error = _error;
            return _catalog;
        }

        private static Hash128 GetDependencyHash(TextAsset? source)
        {
            if (!source)
            {
                return default;
            }

            string assetPath = AssetDatabase.GetAssetPath(source);
            return string.IsNullOrEmpty(assetPath)
                ? default
                : AssetDatabase.GetAssetDependencyHash(assetPath);
        }

        private static void Reload(
            I18nCatalogAsset? catalogAsset,
            TextAsset? source,
            Hash128 sourceHash)
        {
            _isInitialized = true;
            _catalogInstanceId = catalogAsset ? catalogAsset.GetInstanceID() : 0;
            _sourceInstanceId = source ? source.GetInstanceID() : 0;
            _sourceHash = sourceHash;
            _catalog = null;
            _error = null;

            if (!catalogAsset)
            {
                _error = "Active localization catalog is not configured.";
                return;
            }

            if (!source)
            {
                _error = "Active localization catalog has no source JSON.";
                return;
            }

            try
            {
                _catalog = catalogAsset.Deserialize();
            }
            catch (Exception exception)
            {
                _error = $"Localization catalog cannot be read: {exception.Message}";
            }
        }
    }
}
