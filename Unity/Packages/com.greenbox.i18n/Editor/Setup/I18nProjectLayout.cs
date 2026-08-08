#nullable enable

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
        internal const string CatalogAssetPath = RootFolderPath + "/localization.asset";
        internal const string ReadmePath = RootFolderPath + "/readme.md";

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
                "This folder was created automatically by GreenBox I18n.\n" +
                "\n" +
                "- `localization.json` is the editable localization source. Prefer the GreenBox Web Editor or CLI.\n" +
                "- `localization.asset` is managed by GreenBox I18n. Do not edit, replace, or recreate it manually.\n" +
                "- You may move or rename this folder and its files. Prefer doing so inside Unity.\n" +
                "- Keep the accompanying `.meta` files and commit them to version control.\n" +
                "- If you edit the JSON manually, preserve existing entry IDs and validate the result.\n";
        }
    }
}
