namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Describes personal editor preferences stored for the current operating-system user.
/// </summary>
/// <param name="ReopenLastCatalog">Whether the last catalog should be reopened on startup.</param>
/// <param name="WarnUnusedEntries">Whether reliably unused entries are marked in the hierarchy.</param>
/// <param name="WarnIncompleteEntries">Whether entries with incomplete localized content are marked.</param>
/// <param name="LastCatalogPath">The most recently opened catalog path.</param>
/// <param name="RestoreError">The error produced by the latest startup restore attempt.</param>
public sealed record EditorPreferencesResponse(
    bool ReopenLastCatalog,
    bool WarnUnusedEntries,
    bool WarnIncompleteEntries,
    string? LastCatalogPath,
    string? RestoreError);
