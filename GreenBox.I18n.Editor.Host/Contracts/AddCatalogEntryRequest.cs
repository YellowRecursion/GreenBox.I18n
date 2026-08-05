namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Requests creation of a catalog entry at a logical path.
/// </summary>
/// <param name="Path">The full logical path of the new entry.</param>
public sealed record AddCatalogEntryRequest(string Path);
