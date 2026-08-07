using GreenBox.I18n.Editor.Host.Contracts;

namespace GreenBox.I18n.Editor.Host.Infrastructure;

/// <summary>
/// Reports the Unity project associated with a catalog and whether its Editor process is alive.
/// </summary>
public sealed class UnityProjectPresenceService
{
    private const string RelativeLeasePath = "Library/GreenBox.I18n/unity-editor.lock";
    private readonly UnityProjectLocator _projectLocator;

    /// <summary>
    /// Creates a Unity project-presence service.
    /// </summary>
    public UnityProjectPresenceService(UnityProjectLocator projectLocator)
    {
        _projectLocator = projectLocator;
    }

    /// <summary>
    /// Gets the project and live Unity Editor state for the open catalog.
    /// </summary>
    public UnityProjectStatusResponse GetStatus(string? catalogPath)
    {
        string? projectPath = _projectLocator.FindProjectRoot(catalogPath);
        if (projectPath == null)
        {
            return new UnityProjectStatusResponse(false, null, null, false);
        }

        string leasePath = Path.Combine(
            projectPath,
            RelativeLeasePath.Replace('/', Path.DirectorySeparatorChar));
        return new UnityProjectStatusResponse(
            true,
            new DirectoryInfo(projectPath).Name,
            projectPath,
            IsLeaseHeld(leasePath));
    }

    private static bool IsLeaseHeld(string leasePath)
    {
        if (!File.Exists(leasePath))
        {
            return false;
        }

        try
        {
            using var probe = new FileStream(
                leasePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
