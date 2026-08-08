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
        private const int CurrentSetupVersion = 1;

        internal static event Action? ActiveCatalogChanged;

        [UnityEngine.SerializeField]
        private int _setupVersion;

        [UnityEngine.SerializeField]
        private string _activeCatalogGuid = string.Empty;

        [UnityEngine.SerializeField]
        private string _activeCatalogPath = string.Empty;

        /// <summary>
        /// Gets the catalog managed for this Unity project.
        /// </summary>
        internal I18nCatalogAsset? ActiveCatalog => ResolveActiveCatalog();

        /// <summary>
        /// Gets whether automatic project setup has already completed.
        /// </summary>
        internal bool IsSetupComplete => _setupVersion >= CurrentSetupVersion;

        /// <summary>
        /// Records the project catalog selected by the setup workflow.
        /// </summary>
        internal void ConfigureActiveCatalog(I18nCatalogAsset activeCatalog)
        {
            if (!activeCatalog)
            {
                throw new ArgumentNullException(nameof(activeCatalog));
            }

            string assetPath = AssetDatabase.GetAssetPath(activeCatalog);
            string activeCatalogGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(activeCatalogGuid))
            {
                throw new ArgumentException(
                    "The project catalog must be saved in the Unity project.",
                    nameof(activeCatalog));
            }

            string activeCatalogPath = GetSourceCatalogPath(activeCatalog);
            bool changed =
                _setupVersion != CurrentSetupVersion ||
                !string.Equals(_activeCatalogGuid, activeCatalogGuid, StringComparison.Ordinal) ||
                !string.Equals(_activeCatalogPath, activeCatalogPath, StringComparison.Ordinal);
            if (!changed)
            {
                return;
            }

            _setupVersion = CurrentSetupVersion;
            _activeCatalogGuid = activeCatalogGuid;
            _activeCatalogPath = activeCatalogPath;
            Save(true);
            ActiveCatalogChanged?.Invoke();
        }

        /// <summary>
        /// Migrates valid settings written before automatic setup was introduced.
        /// </summary>
        internal void EnsureCurrentSetupVersion()
        {
            if (_setupVersion >= CurrentSetupVersion)
            {
                return;
            }

            _setupVersion = CurrentSetupVersion;
            Save(true);
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
