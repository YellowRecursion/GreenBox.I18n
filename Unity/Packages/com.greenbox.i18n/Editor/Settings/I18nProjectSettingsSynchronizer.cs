#nullable enable

using UnityEditor;

namespace GreenBox.I18n.Unity.Editor.Settings
{
    /// <summary>
    /// Keeps paths exposed to external tooling synchronized with Unity asset moves and edits.
    /// </summary>
    [InitializeOnLoad]
    internal static class I18nProjectSettingsSynchronizer
    {
        static I18nProjectSettingsSynchronizer()
        {
            EditorApplication.delayCall += Refresh;
            EditorApplication.projectChanged += Refresh;
        }

        private static void Refresh()
        {
            I18nProjectSettings.instance.RefreshActiveCatalogPath();
        }
    }
}
