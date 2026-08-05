using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Editor.Host.Editor;
using GreenBox.I18n.Editor.Host.Infrastructure;

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
        catalogEndpoints.MapGet("/source-status", GetSourceStatus);
        catalogEndpoints.MapPost("/entries", AddEntry);
        catalogEndpoints.MapPost("/entries/remove", RemoveEntries);
        catalogEndpoints.MapPost("/entries/move", MoveEntries);
        catalogEndpoints.MapDelete("/entries/{id:long}", RemoveEntry);
        catalogEndpoints.MapPost("/save", Save);
        catalogEndpoints.MapPost("/revert", RevertAsync);
        catalogEndpoints.MapPost("/merge-source", MergeSourceAsync);
        return endpoints;
    }

    private static IResult GetSourceStatus(EditorSession session)
    {
        if (!session.GetSnapshot().HasCatalog)
        {
            return Results.NotFound(new EditorErrorResponse(
                EditorErrorCodes.CatalogNotOpen,
                "No catalog is open in the editor session."));
        }

        return Results.Ok(session.GetSourceStatus());
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

    private static IResult RemoveEntry(long id, EditorSession session)
    {
        CatalogEditResult result = session.RemoveEntry(id);
        return result.Error == null
            ? Results.Ok(result.Catalog)
            : Results.Json(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    private static IResult RemoveEntries(RemoveCatalogEntriesRequest request, EditorSession session)
    {
        CatalogEditResult result = session.RemoveEntries(request.Ids);
        return result.Error == null
            ? Results.Ok(result.Catalog)
            : Results.Json(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    private static IResult MoveEntries(MoveCatalogEntriesRequest request, EditorSession session)
    {
        CatalogEditResult result = session.MoveEntries(request.Moves);
        return result.Error == null
            ? Results.Ok(result.Catalog)
            : Results.Json(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    private static IResult Save(SaveCatalogRequest request, EditorSession session)
    {
        CatalogEditResult result = session.Save(request.OverwriteExternalChanges);
        if (result.Error == null)
        {
            return Results.Ok(result.Catalog);
        }

        int statusCode = result.Error.Code == EditorErrorCodes.CatalogChangedExternally
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status422UnprocessableEntity;
        return Results.Json(result.Error, statusCode: statusCode);
    }

    private static async Task<IResult> RevertAsync(
        EditorSession session,
        CatalogFileLoader loader,
        CancellationToken cancellationToken)
    {
        string? path = session.GetSnapshot().CatalogPath;
        if (path == null)
        {
            return Results.NotFound(new EditorErrorResponse(
                EditorErrorCodes.CatalogNotOpen,
                "No catalog is open in the editor session."));
        }

        CatalogLoadResult loadResult = await loader.LoadAsync(path, cancellationToken);
        if (!loadResult.IsSuccess)
        {
            return Results.Json(loadResult.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        session.Open(loadResult.CatalogPath!, loadResult.Catalog!, loadResult.ContentHash!);
        return Results.Ok(session.GetCatalogSnapshot());
    }

    private static async Task<IResult> MergeSourceAsync(
        EditorSession session,
        CatalogFileLoader loader,
        CancellationToken cancellationToken)
    {
        string? path = session.GetSnapshot().CatalogPath;
        if (path == null)
        {
            return Results.NotFound(new EditorErrorResponse(
                EditorErrorCodes.CatalogNotOpen,
                "No catalog is open in the editor session."));
        }

        CatalogLoadResult loadResult = await loader.LoadAsync(path, cancellationToken);
        if (!loadResult.IsSuccess)
        {
            return Results.Json(loadResult.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        CatalogSourceMergeResult result = session.MergeSource(loadResult.Catalog!, loadResult.ContentHash!);
        if (result.Error == null)
        {
            return Results.Ok(result.Catalog);
        }

        return Results.Json(
            new CatalogMergeErrorResponse(
                result.Error.Code,
                result.Error.Message,
                result.Conflicts.Select(conflict =>
                    new CatalogMergeConflictResponse(conflict.JsonPath, conflict.Message)).ToArray()),
            statusCode: StatusCodes.Status409Conflict);
    }
}
