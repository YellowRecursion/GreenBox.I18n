using GreenBox.I18n.Editor.Host.Contracts;

namespace GreenBox.I18n.Editor.Host.Infrastructure;

/// <summary>
/// Contains either a loaded catalog and its normalized path or an operation error.
/// </summary>
public sealed class CatalogLoadResult
{
    private CatalogLoadResult(I18nCatalog? catalog, string? catalogPath, EditorErrorResponse? error)
    {
        Catalog = catalog;
        CatalogPath = catalogPath;
        Error = error;
    }

    /// <summary>Gets the loaded catalog when the operation succeeded.</summary>
    public I18nCatalog? Catalog { get; }

    /// <summary>Gets the normalized absolute catalog path when the operation succeeded.</summary>
    public string? CatalogPath { get; }

    /// <summary>Gets the operation error when loading failed.</summary>
    public EditorErrorResponse? Error { get; }

    /// <summary>Gets whether the catalog was loaded successfully.</summary>
    public bool IsSuccess => Catalog != null;

    /// <summary>Creates a successful catalog load result.</summary>
    public static CatalogLoadResult Success(I18nCatalog catalog, string catalogPath)
    {
        return new CatalogLoadResult(catalog, catalogPath, null);
    }

    /// <summary>Creates a failed catalog load result.</summary>
    public static CatalogLoadResult Failure(string code, string message)
    {
        return new CatalogLoadResult(null, null, new EditorErrorResponse(code, message));
    }
}
