namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Describes an update to personal editor preferences.
/// </summary>
/// <param name="ReopenLastCatalog">Whether the last catalog should be reopened on startup.</param>
/// <param name="WarnUnusedEntries">Whether reliably unused entries are marked in the hierarchy.</param>
/// <param name="WarnIncompleteEntries">Whether entries with incomplete localized content are marked.</param>
public sealed record UpdateEditorPreferencesRequest(
    bool ReopenLastCatalog,
    bool WarnUnusedEntries,
    bool WarnIncompleteEntries);
