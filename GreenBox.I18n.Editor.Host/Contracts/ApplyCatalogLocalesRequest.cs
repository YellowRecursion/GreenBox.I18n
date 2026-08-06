namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Requests an atomic replacement of locale settings and the default locale.
/// </summary>
/// <param name="ExpectedRevision">The working-copy revision on which the edit is based.</param>
/// <param name="DefaultLocale">The locale identifier used as the catalog default.</param>
/// <param name="Locales">The complete locale definitions in editor display order.</param>
public sealed record ApplyCatalogLocalesRequest(
    long ExpectedRevision,
    string DefaultLocale,
    IReadOnlyList<CatalogLocaleEditRequest> Locales);

/// <summary>
/// Describes the complete editable state of a catalog locale.
/// </summary>
/// <param name="Id">The stable locale identifier.</param>
/// <param name="DisplayName">The human-readable locale name.</param>
/// <param name="Culture">The .NET culture name.</param>
/// <param name="Fallback">The optional fallback locale identifier.</param>
/// <param name="Icon">The optional locale icon asset.</param>
public sealed record CatalogLocaleEditRequest(
    string Id,
    string DisplayName,
    string Culture,
    string? Fallback,
    CatalogAssetReferenceEditRequest? Icon);
