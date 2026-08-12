namespace GreenBox.I18n.Workspace.Contracts;

/// <summary>
/// Requests atomic path changes for several catalog entries.
/// </summary>
/// <param name="Moves">The stable entry IDs and destination paths.</param>
public sealed record MoveCatalogEntriesRequest(IReadOnlyList<MoveCatalogEntryRequest> Moves);

/// <summary>
/// Describes one entry path change in an atomic move request.
/// </summary>
/// <param name="Id">The decimal stable entry ID.</param>
/// <param name="Path">The destination path.</param>
public sealed record MoveCatalogEntryRequest(string Id, string Path);
