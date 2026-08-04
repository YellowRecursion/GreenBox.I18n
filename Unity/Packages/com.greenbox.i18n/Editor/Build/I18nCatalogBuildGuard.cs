#nullable enable

using GreenBox.I18n.Unity.Editor.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace GreenBox.I18n.Unity.Editor.Build
{
    /// <summary>
    /// Prevents player builds from using missing or stale localization runtime data.
    /// </summary>
    public sealed class I18nCatalogBuildGuard : IPreprocessBuildWithReport
    {
        /// <inheritdoc />
        public int callbackOrder => 0;

        /// <inheritdoc />
        public void OnPreprocessBuild(BuildReport report)
        {
            I18nCatalogAsset? catalogAsset = I18nProjectSettings.instance.ActiveCatalog;
            if (!catalogAsset)
            {
                throw new BuildFailedException(
                    "GreenBox I18n build validation failed: Active Catalog is not configured in " +
                    "Project Settings > GreenBox I18n.");
            }

            I18nCatalogCompilationState state = I18nCatalogCompiler.GetState(catalogAsset);
            switch (state)
            {
                case I18nCatalogCompilationState.NotCompiled:
                    throw new BuildFailedException(
                        $"GreenBox I18n build validation failed: catalog '{catalogAsset.name}' is not compiled. " +
                        "Assign its source JSON and click Compile Catalog.");
                case I18nCatalogCompilationState.OutOfDate:
                    throw new BuildFailedException(
                        $"GreenBox I18n build validation failed: catalog '{catalogAsset.name}' is out of date. " +
                        "Reimport its source JSON or click Compile Catalog.");
                case I18nCatalogCompilationState.UpToDate:
                    return;
            }

            throw new BuildFailedException(
                $"GreenBox I18n build validation failed: catalog '{catalogAsset.name}' has an unknown state.");
        }
    }
}
