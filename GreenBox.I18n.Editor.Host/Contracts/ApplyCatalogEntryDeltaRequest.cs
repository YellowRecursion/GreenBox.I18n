namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Requests an atomic replacement and removal of catalog entries by stable ID.
/// </summary>
/// <param name="ExpectedRevision">The working-copy revision on which the delta is based.</param>
/// <param name="Entries">The complete entry values to add or replace.</param>
/// <param name="RemovedIds">The stable IDs that must be absent after the operation.</param>
public sealed record ApplyCatalogEntryDeltaRequest(
    long ExpectedRevision,
    IReadOnlyList<CatalogEntryEditRequest> Entries,
    IReadOnlyList<string> RemovedIds);

/// <summary>
/// Describes the complete editable state of a catalog entry.
/// </summary>
/// <param name="Id">The stable entry ID.</param>
/// <param name="Path">The complete logical entry path.</param>
/// <param name="Comment">The optional developer and translator note.</param>
/// <param name="Locales">The localized values indexed by locale ID.</param>
public sealed record CatalogEntryEditRequest(
    string Id,
    string Path,
    string? Comment,
    IReadOnlyDictionary<string, CatalogLocaleValueEditRequest> Locales);

/// <summary>
/// Describes the complete editable value for one locale.
/// </summary>
/// <param name="Text">The optional localized text.</param>
/// <param name="Asset">The optional localized asset reference.</param>
public sealed record CatalogLocaleValueEditRequest(
    string? Text,
    CatalogAssetReferenceEditRequest? Asset);

/// <summary>
/// Describes an editable Unity asset reference.
/// </summary>
/// <param name="AssetGuid">The Unity asset GUID.</param>
/// <param name="LocalFileId">The optional Unity sub-asset local file identifier.</param>
public sealed record CatalogAssetReferenceEditRequest(string AssetGuid, string? LocalFileId);
