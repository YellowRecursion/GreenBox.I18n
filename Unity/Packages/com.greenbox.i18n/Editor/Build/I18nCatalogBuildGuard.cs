#nullable enable

using GreenBox.I18n.Unity.Editor.Settings;
using GreenBox.I18n.Unity.Editor.Setup;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace GreenBox.I18n.Unity.Editor.Build
{
    /// <summary>
    /// Regenerates runtime data before a build and rejects invalid source catalogs.
    /// </summary>
    public sealed class I18nCatalogBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!I18nProjectSetup.TryRepair(out string setupError))
            {
                throw new BuildFailedException(
                    "GreenBox I18n could not prepare generated runtime data. " + setupError);
            }

            I18nCatalogAsset? catalogAsset = I18nProjectSettings.instance.ProjectCatalog;
            if (!catalogAsset)
            {
                throw new BuildFailedException(
                    "GreenBox I18n generated runtime data is unavailable after project repair.");
            }

            if (I18nCatalogCompiler.GetState(catalogAsset) ==
                I18nCatalogCompilationState.UpToDate)
            {
                return;
            }

            I18nCatalogCompilationResult result = I18nCatalogCompiler.Compile(catalogAsset);
            if (!result.IsSuccess)
            {
                throw new BuildFailedException(
                    "GreenBox I18n could not compile localization.json. " +
                    "Open the generated runtime asset for validation details.");
            }
        }
    }
}
