using GreenBox.I18n.Editor.Host.Editor;
using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Editor.Host.Infrastructure;

namespace GreenBox.I18n.Editor.Host.Endpoints;

/// <summary>
/// Registers endpoints related to the editor session.
/// </summary>
public static class EditorSessionEndpoints
{
    /// <summary>
    /// Maps editor session endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The supplied endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapEditorSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder sessionEndpoints = endpoints.MapGroup("/api/session");

        sessionEndpoints.MapGet(string.Empty, (EditorSession session) => session.GetSnapshot());
        sessionEndpoints.MapPost("/open", OpenCatalogAsync);

        return endpoints;
    }

    private static async Task<IResult> OpenCatalogAsync(
        OpenCatalogRequest request,
        CatalogFileLoader loader,
        EditorSession session,
        CancellationToken cancellationToken)
    {
        CatalogLoadResult loadResult = await loader.LoadAsync(request.Path, cancellationToken);
        if (!loadResult.IsSuccess)
        {
            int statusCode = loadResult.Error!.Code == EditorErrorCodes.CatalogNotFound
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status422UnprocessableEntity;

            return Results.Json(loadResult.Error, statusCode: statusCode);
        }

        EditorSessionResponse response = session.Open(loadResult.CatalogPath!, loadResult.Catalog!);
        return Results.Ok(response);
    }
}
