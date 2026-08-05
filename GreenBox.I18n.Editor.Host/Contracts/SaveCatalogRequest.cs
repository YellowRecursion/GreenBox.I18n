namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Requests persistence of the current catalog working copy.
/// </summary>
/// <param name="OverwriteExternalChanges">Whether to overwrite a source file changed externally.</param>
public sealed record SaveCatalogRequest(bool OverwriteExternalChanges);
