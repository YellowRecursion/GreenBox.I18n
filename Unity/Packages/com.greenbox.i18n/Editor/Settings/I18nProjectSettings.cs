#nullable enable

using System;
using GreenBox.I18n.Unity.Editor.Setup;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Settings
{
    /// <summary>
    /// Stores project-wide discovery data for the single GreenBox I18n source catalog.
    /// </summary>
    [FilePath("ProjectSettings/GreenBox.I18n.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class I18nProjectSettings : ScriptableSingleton<I18nProjectSettings>
    {
        private const int CurrentSetupVersion = 3;

        internal static event Action? SourceCatalogChanged;

        [SerializeField]
        private int _setupVersion;

        [SerializeField]
        private string _sourceCatalogGuid = string.Empty;

        [SerializeField]
        private string _sourceCatalogPath = string.Empty;

        /// <summary>
        /// Gets the editable JSON source of truth for this Unity project.
        /// </summary>
        internal TextAsset? SourceCatalog => ResolveSourceCatalog();

        /// <summary>
        /// Gets the generated runtime asset derived from the source catalog location.
        /// </summary>
        internal I18nCatalogAsset? ProjectCatalog
        {
            get
            {
                if (string.IsNullOrEmpty(_sourceCatalogPath))
                {
                    return null;
                }

                string assetPath = I18nProjectLayout.GetCatalogAssetPath(_sourceCatalogPath);
                return AssetDatabase.LoadAssetAtPath<I18nCatalogAsset>(assetPath);
            }
        }

        internal bool IsSetupComplete => _setupVersion >= CurrentSetupVersion;

        /// <summary>
        /// Gets the Unity project-relative source path exposed to external tools.
        /// </summary>
        internal string SourceCatalogPath => _sourceCatalogPath;

        /// <summary>
        /// Records the source catalog. The generated runtime asset is deliberately not persisted.
        /// </summary>
        internal void ConfigureSourceCatalog(TextAsset sourceCatalog)
        {
            if (!sourceCatalog)
            {
                throw new ArgumentNullException(nameof(sourceCatalog));
            }

            string sourcePath = AssetDatabase.GetAssetPath(sourceCatalog);
            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            if (string.IsNullOrEmpty(sourceGuid))
            {
                throw new ArgumentException(
                    "The source catalog must be saved in the Unity project.",
                    nameof(sourceCatalog));
            }

            bool changed =
                _setupVersion != CurrentSetupVersion ||
                !string.Equals(_sourceCatalogGuid, sourceGuid, StringComparison.Ordinal) ||
                !string.Equals(_sourceCatalogPath, sourcePath, StringComparison.Ordinal);
            if (!changed)
            {
                return;
            }

            _setupVersion = CurrentSetupVersion;
            _sourceCatalogGuid = sourceGuid;
            _sourceCatalogPath = sourcePath;
            Save(true);
            SourceCatalogChanged?.Invoke();
        }

        /// <summary>
        /// Updates the external-tool path after Unity moves the source catalog.
        /// </summary>
        internal void RefreshSourceCatalogPath()
        {
            TextAsset? sourceCatalog = ResolveSourceCatalog();
            if (!sourceCatalog)
            {
                return;
            }

            string sourcePath = AssetDatabase.GetAssetPath(sourceCatalog);
            if (string.Equals(_sourceCatalogPath, sourcePath, StringComparison.Ordinal))
            {
                return;
            }

            _sourceCatalogPath = sourcePath;
            Save(true);
            SourceCatalogChanged?.Invoke();
        }

        private TextAsset? ResolveSourceCatalog()
        {
            if (!string.IsNullOrEmpty(_sourceCatalogGuid))
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(_sourceCatalogGuid);
                TextAsset? sourceByGuid = AssetDatabase.LoadAssetAtPath<TextAsset>(guidPath);
                if (sourceByGuid)
                {
                    return sourceByGuid;
                }
            }

            return string.IsNullOrEmpty(_sourceCatalogPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<TextAsset>(_sourceCatalogPath);
        }
    }
}
