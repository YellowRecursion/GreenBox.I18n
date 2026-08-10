#nullable enable

using System.Collections.Generic;
using GreenBox.I18n.Unity.Editor.Setup;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Settings
{
    /// <summary>
    /// Draws the automatically managed GreenBox I18n project state.
    /// </summary>
    internal static class I18nProjectSettingsProvider
    {
        private static string _setupError = string.Empty;

        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/GreenBox/i18n", SettingsScope.Project)
            {
                label = "i18n",
                guiHandler = DrawSettings,
                keywords = new HashSet<string>
                {
                    "GreenBox",
                    "I18n",
                    "Localization",
                    "Catalog"
                }
            };
        }

        private static void DrawSettings(string searchContext)
        {
            I18nProjectSettings settings = I18nProjectSettings.instance;
            TextAsset? sourceCatalog = settings.SourceCatalog;
            I18nCatalogAsset? projectCatalog = settings.ProjectCatalog;

            if (!sourceCatalog || !projectCatalog)
            {
                string message = !sourceCatalog
                    ? "The localization source JSON is missing. Restore it from version control."
                    : "Generated runtime data is missing and will be recreated automatically.";
                EditorGUILayout.HelpBox(message, MessageType.Warning);

                if (!string.IsNullOrEmpty(_setupError))
                {
                    EditorGUILayout.HelpBox(_setupError, MessageType.Error);
                }

                if (GUILayout.Button("Repair Project Files"))
                {
                    _setupError = I18nProjectSetup.TryRepair(out string error)
                        ? string.Empty
                        : error;
                }

                return;
            }

            EditorGUILayout.LabelField(
                new GUIContent(
                    "Source JSON",
                    "The source-controlled localization catalog for this Unity project."),
                new GUIContent(settings.SourceCatalogPath));
            EditorGUILayout.LabelField(
                new GUIContent(
                    "Generated Runtime Data",
                    "Ignored by Git and regenerated automatically from the source JSON."),
                new GUIContent(AssetDatabase.GetAssetPath(projectCatalog)));

            if (GUILayout.Button("Show Source JSON"))
            {
                Selection.activeObject = sourceCatalog;
                EditorGUIUtility.PingObject(sourceCatalog);
            }

        }
    }
}
