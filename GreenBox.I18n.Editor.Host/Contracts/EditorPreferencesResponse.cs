namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Describes personal editor preferences stored for the current operating-system user.
/// </summary>
/// <param name="ReopenLastCatalog">Whether the last catalog should be reopened on startup.</param>
/// <param name="LastCatalogPath">The most recently opened catalog path.</param>
/// <param name="RestoreError">The error produced by the latest startup restore attempt.</param>
public sealed record EditorPreferencesResponse(
    bool ReopenLastCatalog,
    string? LastCatalogPath,
    string? RestoreError);
