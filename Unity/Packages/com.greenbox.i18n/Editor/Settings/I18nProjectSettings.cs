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

        /// <summary>
        /// Gets or sets the catalog used by localization editor tools in this project.
        /// </summary>
        internal I18nCatalogAsset? ActiveCatalog
        {
            get
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(_activeCatalogGuid);
                return AssetDatabase.LoadAssetAtPath<I18nCatalogAsset>(assetPath);
            }
            set
            {
                var assetPath = value ? AssetDatabase.GetAssetPath(value) : string.Empty;
                string activeCatalogGuid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.Equals(_activeCatalogGuid, activeCatalogGuid, StringComparison.Ordinal))
                {
                    return;
                }

                _activeCatalogGuid = activeCatalogGuid;
                Save(true);
                ActiveCatalogChanged?.Invoke();
            }
        }
    }
}
