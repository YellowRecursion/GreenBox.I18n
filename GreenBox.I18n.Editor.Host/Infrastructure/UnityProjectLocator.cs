namespace GreenBox.I18n.Editor.Host.Infrastructure;

/// <summary>
/// Locates the Unity project that contains a catalog file.
/// </summary>
public sealed class UnityProjectLocator
{
    /// <summary>
    /// Finds the containing Unity project by its standard Assets and ProjectSettings directories.
    /// </summary>
    /// <param name="catalogPath">The absolute path of the open catalog.</param>
    /// <returns>The absolute Unity project root, or <see langword="null"/> when the catalog is standalone.</returns>
    public string? FindProjectRoot(string? catalogPath)
    {
        DirectoryInfo? directory = string.IsNullOrWhiteSpace(catalogPath)
            ? null
            : new FileInfo(catalogPath).Directory;

        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Assets")) &&
                Directory.Exists(Path.Combine(directory.FullName, "ProjectSettings")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
