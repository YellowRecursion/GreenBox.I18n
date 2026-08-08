#nullable enable

using System.Collections.Generic;
using GreenBox.I18n.Unity.Editor.Setup;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Settings
{
    /// <summary>
    /// Draws GreenBox I18n settings in the Unity Project Settings window.
    /// </summary>
    internal static class I18nProjectSettingsProvider
    {
        private static string _setupError = string.Empty;

        /// <summary>
        /// Creates the project settings provider discovered by Unity.
        /// </summary>
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
            I18nCatalogAsset? activeCatalog = settings.ActiveCatalog;
            if (!activeCatalog || !activeCatalog.SourceCatalog)
            {
                string message;
                if (!settings.IsSetupComplete)
                {
                    message = "The project catalog has not been created yet.";
                }
                else if (!activeCatalog)
                {
                    message =
                        "The managed project catalog asset is missing. " +
                        "It will not be recreated automatically.";
                }
                else
                {
                    message =
                        "The localization JSON is missing. " +
                        "It will not be recreated automatically.";
                }
                EditorGUILayout.HelpBox(message, MessageType.Warning);

                if (!string.IsNullOrEmpty(_setupError))
                {
                    EditorGUILayout.HelpBox(_setupError, MessageType.Error);
                }

                string buttonLabel = settings.IsSetupComplete
                    ? "Recreate Project Catalog"
                    : "Create Project Catalog";
                if (GUILayout.Button(buttonLabel))
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
                    "The localization source managed for this Unity project."),
                settings.ActiveCatalogPath);
            EditorGUILayout.LabelField(
                new GUIContent(
                    "Unity Asset",
                    "The generated Unity bridge and compiled asset bindings."),
                AssetDatabase.GetAssetPath(activeCatalog));

            if (GUILayout.Button("Show in Project"))
            {
                Object target = activeCatalog.SourceCatalog
                    ? activeCatalog.SourceCatalog
                    : activeCatalog;
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
            }
        }
    }
}
