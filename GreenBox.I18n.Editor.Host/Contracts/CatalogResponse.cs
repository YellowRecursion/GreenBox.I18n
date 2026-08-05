namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Contains an immutable client-facing snapshot of the catalog working copy.
/// </summary>
/// <param name="Revision">The revision of the working copy.</param>
/// <param name="DefaultLocale">The catalog default locale identifier.</param>
/// <param name="Locales">The supported locales in editor display order.</param>
/// <param name="Entries">The catalog entries in canonical order.</param>
public sealed record CatalogResponse(
    long Revision,
    string DefaultLocale,
    IReadOnlyList<CatalogLocaleResponse> Locales,
    IReadOnlyList<CatalogEntryResponse> Entries);

/// <summary>
/// Describes a locale in a catalog snapshot.
/// </summary>
/// <param name="Id">The stable locale identifier.</param>
/// <param name="DisplayName">The human-readable locale name.</param>
/// <param name="Culture">The .NET culture name.</param>
/// <param name="Fallback">The optional fallback locale identifier.</param>
/// <param name="Icon">The optional locale icon asset.</param>
public sealed record CatalogLocaleResponse(
    string Id,
    string DisplayName,
    string Culture,
    string? Fallback,
    CatalogAssetReferenceResponse? Icon);

/// <summary>
/// Describes an entry in a catalog snapshot.
/// </summary>
/// <param name="Id">The stable numeric entry identifier.</param>
/// <param name="Path">The logical entry path.</param>
/// <param name="Comment">The optional developer and translator note.</param>
/// <param name="Locales">The localized values indexed by locale identifier.</param>
public sealed record CatalogEntryResponse(
    string Id,
    string Path,
    string? Comment,
    IReadOnlyDictionary<string, CatalogLocaleValueResponse> Locales);

/// <summary>
/// Describes the localized value of an entry in a catalog snapshot.
/// </summary>
/// <param name="Text">The optional localized text.</param>
/// <param name="Asset">The optional localized asset.</param>
public sealed record CatalogLocaleValueResponse(
    string? Text,
    CatalogAssetReferenceResponse? Asset);

/// <summary>
/// Describes a Unity asset reference in a catalog snapshot.
/// </summary>
/// <param name="AssetGuid">The Unity asset GUID.</param>
/// <param name="LocalFileId">The optional Unity sub-asset local file identifier.</param>
public sealed record CatalogAssetReferenceResponse(string AssetGuid, string? LocalFileId);
