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
#if UNITY_6000_4_OR_NEWER
        private static EntityId _sourceEntityId;
#else
        private static int _sourceInstanceId;
#endif
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
            TextAsset? source = I18nProjectSettings.instance.SourceCatalog;
            Hash128 sourceHash = GetDependencyHash(source);
#if UNITY_6000_4_OR_NEWER
            EntityId sourceEntityId = source ? source.GetEntityId() : default;
#else
            int sourceInstanceId = source ? source.GetInstanceID() : 0;
#endif

            if (!_isInitialized ||
#if UNITY_6000_4_OR_NEWER
                _sourceEntityId != sourceEntityId ||
#else
                _sourceInstanceId != sourceInstanceId ||
#endif
                _sourceHash != sourceHash)
            {
                Reload(source, sourceHash);
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
            TextAsset? source,
            Hash128 sourceHash)
        {
            _isInitialized = true;
#if UNITY_6000_4_OR_NEWER
            _sourceEntityId = source ? source.GetEntityId() : default;
#else
            _sourceInstanceId = source ? source.GetInstanceID() : 0;
#endif
            _sourceHash = sourceHash;
            _catalog = null;
            _error = null;

            if (!source)
            {
                _error = "The project localization source JSON is unavailable.";
                return;
            }

            try
            {
                _catalog = I18nCatalogJson.Deserialize(source.text);
            }
            catch (Exception exception)
            {
                _error = $"Localization catalog cannot be read: {exception.Message}";
            }
        }
    }
}
