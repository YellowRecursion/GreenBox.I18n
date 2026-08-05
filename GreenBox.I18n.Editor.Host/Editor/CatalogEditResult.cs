using GreenBox.I18n.Editor.Host.Contracts;

namespace GreenBox.I18n.Editor.Host.Editor;

/// <summary>
/// Represents the result of an editor working-copy mutation.
/// </summary>
public sealed class CatalogEditResult
{
    private CatalogEditResult(CatalogResponse? catalog, EditorErrorResponse? error)
    {
        Catalog = catalog;
        Error = error;
    }

    /// <summary>
    /// Gets the updated catalog snapshot after a successful operation.
    /// </summary>
    public CatalogResponse? Catalog { get; }

    /// <summary>
    /// Gets the expected operation error after a failed operation.
    /// </summary>
    public EditorErrorResponse? Error { get; }

    /// <summary>
    /// Creates a successful edit result.
    /// </summary>
    /// <param name="catalog">The updated catalog snapshot.</param>
    /// <returns>A successful result.</returns>
    public static CatalogEditResult Success(CatalogResponse catalog)
    {
        return new CatalogEditResult(catalog, null);
    }

    /// <summary>
    /// Creates a failed edit result.
    /// </summary>
    /// <param name="code">The stable machine-readable error code.</param>
    /// <param name="message">The human-readable error message.</param>
    /// <returns>A failed result.</returns>
    public static CatalogEditResult Failure(string code, string message)
    {
        return new CatalogEditResult(null, new EditorErrorResponse(code, message));
    }
}
