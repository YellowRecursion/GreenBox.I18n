using GreenBox.I18n.Editor.Host.Contracts;

namespace GreenBox.I18n.Editor.Host.Editor;

/// <summary>
/// Owns the server-side state of the current editor session.
/// </summary>
public sealed class EditorSession
{
    private readonly Lock _lock = new();
    private I18nCatalog? _catalog;
    private string? _catalogPath;
    private long _revision = 0;

    /// <summary>
    /// Creates an immutable snapshot of the current session state.
    /// </summary>
    /// <returns>The current session snapshot.</returns>
    public EditorSessionResponse GetSnapshot()
    {
        lock (_lock)
        {
            return CreateSnapshot();
        }
    }

    /// <summary>
    /// Creates an immutable client-facing snapshot of the catalog working copy.
    /// </summary>
    /// <returns>The catalog snapshot, or <see langword="null"/> when no catalog is open.</returns>
    public CatalogResponse? GetCatalogSnapshot()
    {
        lock (_lock)
        {
            if (_catalog == null)
            {
                return null;
            }

            I18nValidationResult validation = I18nCatalogValidator.Validate(_catalog);
            return new CatalogResponse(
                _revision,
                _catalog.DefaultLocale,
                _catalog.Locales.Select(CreateLocaleResponse).ToArray(),
                _catalog.Entries.Select(CreateEntryResponse).ToArray(),
                validation.Diagnostics.Select(CreateDiagnosticResponse).ToArray());
        }
    }

    /// <summary>
    /// Replaces the current working copy with a loaded catalog.
    /// </summary>
    /// <param name="catalogPath">The absolute path of the catalog source file.</param>
    /// <param name="catalog">The loaded and validated catalog.</param>
    /// <returns>A snapshot of the updated session state.</returns>
    public EditorSessionResponse Open(string catalogPath, I18nCatalog catalog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogPath);
        ArgumentNullException.ThrowIfNull(catalog);

        lock (_lock)
        {
            _catalogPath = catalogPath;
            _catalog = catalog;
            _revision++;
            return CreateSnapshot();
        }
    }

    private EditorSessionResponse CreateSnapshot()
    {
        return new EditorSessionResponse(
            _catalog != null,
            _revision,
            _catalogPath,
            _catalog?.DefaultLocale,
            _catalog?.Locales?.Count ?? 0,
            _catalog?.Entries?.Count ?? 0);
    }

    private static CatalogLocaleResponse CreateLocaleResponse(I18nLocaleDefinition locale)
    {
        return new CatalogLocaleResponse(
            locale.Id,
            locale.DisplayName,
            locale.Culture,
            locale.Fallback,
            CreateAssetResponse(locale.Icon));
    }

    private static CatalogEntryResponse CreateEntryResponse(I18nEntry entry)
    {
        Dictionary<string, CatalogLocaleValueResponse> locales = entry.Locales.ToDictionary(
            pair => pair.Key,
            pair => new CatalogLocaleValueResponse(
                pair.Value.Text,
                CreateAssetResponse(pair.Value.Asset)),
            StringComparer.Ordinal);

        return new CatalogEntryResponse(entry.Id, entry.Path, entry.Comment, locales);
    }

    private static CatalogAssetReferenceResponse? CreateAssetResponse(I18nAssetReference? asset)
    {
        return asset == null
            ? null
            : new CatalogAssetReferenceResponse(asset.AssetGuid, asset.LocalFileId);
    }

    private static CatalogDiagnosticResponse CreateDiagnosticResponse(I18nValidationDiagnostic diagnostic)
    {
        I18nValidationTarget target = diagnostic.Target;
        CatalogDiagnosticTargetResponse? targetResponse =
            target.EntryId == null && target.EntryPath == null && target.LocaleId == null
                ? null
                : new CatalogDiagnosticTargetResponse(target.EntryId, target.EntryPath, target.LocaleId);

        return new CatalogDiagnosticResponse(
            diagnostic.Code,
            diagnostic.Severity.ToString().ToLowerInvariant(),
            diagnostic.JsonPath,
            diagnostic.Message,
            targetResponse);
    }
}
