#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

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
        internal const string GitIgnorePath = RootFolderPath + "/.gitignore";
        internal const string GitAttributesPath = RootFolderPath + "/.gitattributes";

        internal const string RuntimeCatalogPathSuffix =
            "/Resources/" + I18nCatalogAsset.ResourcesPath + ".asset";

        internal static bool IsRuntimeCatalogPath(string assetPath)
        {
            return assetPath.EndsWith(RuntimeCatalogPathSuffix, StringComparison.Ordinal);
        }

        internal static string GetResourcesFolderPath(string sourceCatalogPath)
        {
            return GetSourceFolderPath(sourceCatalogPath) + "/Resources";
        }

        internal static string GetCatalogAssetPath(string sourceCatalogPath)
        {
            return GetResourcesFolderPath(sourceCatalogPath) +
                   "/" + I18nCatalogAsset.ResourcesPath + ".asset";
        }

        private static string GetSourceFolderPath(string sourceCatalogPath)
        {
            string? folderPath = Path.GetDirectoryName(sourceCatalogPath);
            if (string.IsNullOrEmpty(folderPath))
            {
                throw new ArgumentException(
                    "The source catalog must have a Unity project-relative path.",
                    nameof(sourceCatalogPath));
            }

            return folderPath.Replace('\\', '/');
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
                "- `Resources/greenbox-i18n.asset` is generated binary runtime data and is excluded from Git. The player does not parse JSON or MessageFormat. Do not edit or reference it manually.\n" +
                "- `.gitignore` and `.gitattributes` keep generated data out of Git and normalize text files across operating systems. Commit both files to your project repository.\n" +
                "- To enable structural Git merges, install GreenBox Desktop Tools and follow Preferences > GreenBox > i18n.\n" +
                "- If you must edit `localization.json` manually, preserve existing entry IDs and validate the result afterwards.\n";
        }

        internal static string CreateGitIgnore()
        {
            // TODO: Add a Unity Version Control integration instead of treating these Git files
            // as universal VCS configuration. It must generate the appropriate ignore and EOL
            // rules and configure the same three-way CLI merge through UVCS's external merge tool.
            return "/Resources/\n/Resources.meta\n";
        }

        internal static string CreateGitAttributes()
        {
            return
                "/localization.json text eol=lf merge=greenbox-i18n\n" +
                "/readme.md text eol=lf\n" +
                "/.gitignore text eol=lf\n" +
                "/.gitattributes text eol=lf\n";
        }
    }
}
