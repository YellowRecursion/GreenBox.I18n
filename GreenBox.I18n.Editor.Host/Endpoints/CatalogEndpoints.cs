using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Editor.Host.Editor;

namespace GreenBox.I18n.Editor.Host.Endpoints;

/// <summary>
/// Registers endpoints that expose the current catalog working copy.
/// </summary>
public static class CatalogEndpoints
{
    /// <summary>
    /// Maps catalog endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The supplied endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder catalogEndpoints = endpoints.MapGroup("/api/catalog");
        catalogEndpoints.MapGet(string.Empty, GetCatalog);
        catalogEndpoints.MapPost("/entries", AddEntry);
        return endpoints;
    }

    private static IResult GetCatalog(EditorSession session)
    {
        CatalogResponse? catalog = session.GetCatalogSnapshot();
        return catalog == null
            ? Results.NotFound(new EditorErrorResponse(
                EditorErrorCodes.CatalogNotOpen,
                "No catalog is open in the editor session."))
            : Results.Ok(catalog);
    }

    private static IResult AddEntry(AddCatalogEntryRequest request, EditorSession session)
    {
        CatalogEditResult result = session.AddEntry(request.Path);
        return result.Error == null
            ? Results.Ok(result.Catalog)
            : Results.Json(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
    }
}
