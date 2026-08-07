#nullable enable

using System;
using System.IO;

namespace GreenBox.I18n.Usage.Analysis
{
    /// <summary>
    /// Converts compiler and symbol paths into Unity project asset paths without using Unity APIs.
    /// </summary>
    internal static class I18nUsagePath
    {
        internal static bool IsAssetPath(string path, string projectRoot)
        {
            return TryGetAssetPath(path, projectRoot, out _);
        }

        internal static bool TryGetAssetPath(
            string path,
            string projectRoot,
            out string assetPath)
        {
            assetPath = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string localPath = path;
            if (Uri.TryCreate(path, UriKind.Absolute, out Uri? uri) && uri.IsFile)
            {
                localPath = uri.LocalPath;
            }

            string fullPath = Resolve(localPath, projectRoot).Replace('\\', '/');
            string assetsRoot = Path.Combine(projectRoot, "Assets").Replace('\\', '/').TrimEnd('/');
            string assetsPrefix = assetsRoot + "/";
            if (!fullPath.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            assetPath = "Assets/" + fullPath.Substring(assetsPrefix.Length);
            return true;
        }

        internal static string Resolve(string path, string projectRoot)
        {
            return Path.GetFullPath(Path.IsPathRooted(path)
                ? path
                : Path.Combine(projectRoot, path));
        }
    }
}
