#nullable enable

using System;
using UnityEditor;

namespace GreenBox.I18n.Unity.Editor.Settings
{
    /// <summary>
    /// Stores project-wide editor settings for GreenBox I18n.
    /// </summary>
    [FilePath("ProjectSettings/GreenBox.I18n.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class I18nProjectSettings : ScriptableSingleton<I18nProjectSettings>
    {
        internal static event Action? ActiveCatalogChanged;

        [UnityEngine.SerializeField]
        private string _activeCatalogGuid = string.Empty;

        [UnityEngine.SerializeField]
        private string _activeCatalogPath = string.Empty;

        /// <summary>
        /// Gets or sets the catalog used by localization editor tools in this project.
        /// </summary>
        internal I18nCatalogAsset? ActiveCatalog
        {
            get
            {
                return ResolveActiveCatalog();
            }
            set
            {
                var assetPath = value ? AssetDatabase.GetAssetPath(value) : string.Empty;
                string activeCatalogGuid = AssetDatabase.AssetPathToGUID(assetPath);
                string activeCatalogPath = GetSourceCatalogPath(value);
                if (string.Equals(_activeCatalogGuid, activeCatalogGuid, StringComparison.Ordinal) &&
                    string.Equals(_activeCatalogPath, activeCatalogPath, StringComparison.Ordinal))
                {
                    return;
                }

                _activeCatalogGuid = activeCatalogGuid;
                _activeCatalogPath = activeCatalogPath;
                Save(true);
                ActiveCatalogChanged?.Invoke();
            }
        }

        /// <summary>
        /// Gets the Unity project-relative path to the active source catalog JSON.
        /// This serialized value is also a lightweight discovery contract for external tools.
        /// </summary>
        internal string ActiveCatalogPath => _activeCatalogPath;

        /// <summary>
        /// Updates the stored source path after the active catalog or its source asset moves.
        /// </summary>
        internal void RefreshActiveCatalogPath()
        {
            string activeCatalogPath = GetSourceCatalogPath(ResolveActiveCatalog());
            if (string.Equals(_activeCatalogPath, activeCatalogPath, StringComparison.Ordinal))
            {
                return;
            }

            _activeCatalogPath = activeCatalogPath;
            Save(true);
            ActiveCatalogChanged?.Invoke();
        }

        private I18nCatalogAsset? ResolveActiveCatalog()
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(_activeCatalogGuid);
            return AssetDatabase.LoadAssetAtPath<I18nCatalogAsset>(assetPath);
        }

        private static string GetSourceCatalogPath(I18nCatalogAsset? catalogAsset)
        {
            if (!catalogAsset || !catalogAsset.SourceCatalog)
            {
                return string.Empty;
            }

            return AssetDatabase.GetAssetPath(catalogAsset.SourceCatalog);
        }
    }
}
