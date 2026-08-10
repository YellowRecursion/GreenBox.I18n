#nullable enable

using GreenBox.I18n.Unity.Editor.Diagnostics;
using GreenBox.I18n.Unity.Editor.Settings;
using UnityEditor;

namespace GreenBox.I18n.Unity.Editor.Setup
{
    /// <summary>
    /// Performs the one-time project catalog setup after the package is installed.
    /// </summary>
    [InitializeOnLoad]
    internal static class I18nProjectBootstrap
    {
        static I18nProjectBootstrap()
        {
            EditorApplication.delayCall += EnsureInitialized;
        }

        private static void EnsureInitialized()
        {
            I18nProjectSettings settings = I18nProjectSettings.instance;
            I18nCatalogAsset? activeCatalog = settings.ActiveCatalog;
            if (settings.IsSetupComplete &&
                activeCatalog &&
                activeCatalog.SourceCatalog &&
                I18nProjectLayout.IsRuntimeCatalogPath(
                    AssetDatabase.GetAssetPath(activeCatalog)))
            {
                return;
            }

            if (!I18nProjectSetup.TryRepair(out string error))
            {
                I18nLog.Warning("Automatic project catalog setup failed. " + error);
            }
        }
    }
}
