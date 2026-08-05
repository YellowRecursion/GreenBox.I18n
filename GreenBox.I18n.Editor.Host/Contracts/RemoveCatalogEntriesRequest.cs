namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Requests removal of catalog entries by stable numeric ID.
/// </summary>
/// <param name="Ids">The decimal entry IDs to remove as one editor operation.</param>
public sealed record RemoveCatalogEntriesRequest(IReadOnlyList<string> Ids);
