#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Settings
{
    /// <summary>
    /// Draws GreenBox I18n settings in the Unity Project Settings window.
    /// </summary>
    internal static class I18nProjectSettingsProvider
    {
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
            var settings = I18nProjectSettings.instance;

            EditorGUI.BeginChangeCheck();
            var activeCatalog = (I18nCatalogAsset?)EditorGUILayout.ObjectField(
                new GUIContent(
                    "Active Catalog",
                    "Catalog used by localization key pickers and other editor tools."),
                settings.ActiveCatalog,
                typeof(I18nCatalogAsset),
                false);

            if (EditorGUI.EndChangeCheck())
            {
                settings.ActiveCatalog = activeCatalog;
            }
        }
    }
}
