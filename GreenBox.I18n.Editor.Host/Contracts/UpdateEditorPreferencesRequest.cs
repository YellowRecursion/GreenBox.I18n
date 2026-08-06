namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Describes an update to personal editor preferences.
/// </summary>
/// <param name="ReopenLastCatalog">Whether the last catalog should be reopened on startup.</param>
public sealed record UpdateEditorPreferencesRequest(bool ReopenLastCatalog);
