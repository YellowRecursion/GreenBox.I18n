namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Describes the state exposed to an editor client.
/// </summary>
/// <param name="HasCatalog">Whether a localization catalog is loaded.</param>
/// <param name="Revision">The revision of the server-side working copy.</param>
/// <param name="CatalogPath">The absolute path of the loaded catalog.</param>
/// <param name="DefaultLocale">The default locale of the loaded catalog.</param>
/// <param name="LocaleCount">The number of locales in the loaded catalog.</param>
/// <param name="EntryCount">The number of entries in the loaded catalog.</param>
public sealed record EditorSessionResponse(
    bool HasCatalog,
    long Revision,
    string? CatalogPath,
    string? DefaultLocale,
    int LocaleCount,
    int EntryCount);
