#nullable enable

using System;
using System.Collections.Generic;

namespace GreenBox.I18n.Unity.Editor.Setup
{
    /// <summary>
    /// Defines the default project files created by GreenBox I18n.
    /// Their paths are defaults only; Unity GUID references keep working after moves.
    /// </summary>
    internal static class I18nProjectLayout
    {
        internal const string RootFolderPath = "Assets/GreenBox.I18n";
        internal const string SourceCatalogPath = RootFolderPath + "/localization.json";
        internal const string ResourcesFolderPath = RootFolderPath + "/Resources";
        internal const string CatalogAssetPath =
            ResourcesFolderPath + "/" + I18nCatalogAsset.ResourcesPath + ".asset";
        internal const string ReadmePath = RootFolderPath + "/readme.md";

        internal const string RuntimeCatalogPathSuffix =
            "/Resources/" + I18nCatalogAsset.ResourcesPath + ".asset";

        internal static bool IsRuntimeCatalogPath(string assetPath)
        {
            return assetPath.EndsWith(RuntimeCatalogPathSuffix, StringComparison.Ordinal);
        }

        internal static string CreateInitialCatalogJson()
        {
            return I18nCatalogJson.Serialize(new I18nCatalog
            {
                DefaultLocale = "en",
                Locales = new List<I18nLocaleDefinition>
                {
                    new()
                    {
                        Id = "en",
                        DisplayName = "English",
                        Culture = "en-US",
                    },
                },
            });
        }

        internal static string CreateReadme()
        {
            return
                "# GreenBox I18n project files\n" +
                "\n" +
                "This folder is created and maintained automatically by GreenBox I18n.\n" +
                "\n" +
                "## Folder contract\n" +
                "\n" +
                "- You may move this whole folder anywhere inside `Assets`. Move it through Unity so its `.meta` file is preserved.\n" +
                "- Keep the `GreenBox.I18n` folder name. The runtime does not depend on it, but tools and developers use it to recognize the folder.\n" +
                "- Do not rename, move, replace, or delete files and folders inside it. GreenBox I18n validates and repairs the managed runtime layout.\n" +
                "\n" +
                "## Files\n" +
                "\n" +
                "- `localization.json` is the editable source of truth. Prefer the GreenBox Web Editor or CLI.\n" +
                "- `Resources/greenbox-i18n.asset` is generated runtime data. Do not edit or reference it manually.\n" +
                "- If you must edit `localization.json` manually, preserve existing entry IDs and validate the result afterwards.\n";
        }
    }
}
