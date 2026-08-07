namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Describes the Unity project associated with the open catalog and its live Editor presence.
/// </summary>
/// <param name="IsUnityProject">Whether the open catalog belongs to a Unity project.</param>
/// <param name="ProjectName">The Unity project directory name.</param>
/// <param name="ProjectPath">The absolute Unity project path.</param>
/// <param name="IsEditorOnline">Whether a Unity Editor process currently holds the project lease.</param>
public sealed record UnityProjectStatusResponse(
    bool IsUnityProject,
    string? ProjectName,
    string? ProjectPath,
    bool IsEditorOnline);
