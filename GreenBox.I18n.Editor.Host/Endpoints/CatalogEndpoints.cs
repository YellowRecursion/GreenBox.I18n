using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Editor.Host.Editor;
using GreenBox.I18n.Editor.Host.Infrastructure;
using GreenBox.I18n.Workspace;
using GreenBox.I18n.Workspace.Contracts;

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
        catalogEndpoints.MapPost("/entries/apply-delta", ApplyEntryDelta);
        catalogEndpoints.MapPost("/locales/apply", ApplyLocales);
        catalogEndpoints.MapDelete("/entries/{id:long}", RemoveEntry);
        catalogEndpoints.MapPost("/save", Save);
        catalogEndpoints.MapPost("/revert", RevertAsync);
        catalogEndpoints.MapPost("/merge-source", MergeSourceAsync);
        return endpoints;
    }

    private static IResult GetSourceStatus(CatalogWorkspace session)
    {
        if (!session.GetSnapshot().HasCatalog)
        {
            return Results.NotFound(new EditorErrorResponse(
                WorkspaceErrorCodes.CatalogNotOpen,
                "No catalog is open in the editor session."));
        }

        return Results.Ok(session.GetSourceStatus());
    }

    private static IResult GetCatalog(CatalogWorkspace session)
    {
        CatalogResponse? catalog = session.GetCatalogSnapshot();
        return catalog == null
            ? Results.NotFound(new EditorErrorResponse(
                WorkspaceErrorCodes.CatalogNotOpen,
                "No catalog is open in the editor session."))
            : Results.Ok(catalog);
    }

    private static IResult AddEntry(AddCatalogEntryRequest request, CatalogWorkspace session)
    {
        CatalogEditResult result = session.AddEntry(request.Path);
        return result.Error == null
            ? Results.Ok(result.Catalog)
            : Results.Json(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    private static IResult RemoveEntry(long id, CatalogWorkspace session)
    {
        CatalogEditResult result = session.RemoveEntry(id);
        return result.Error == null
            ? Results.Ok(result.Catalog)
            : Results.Json(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    private static IResult RemoveEntries(RemoveCatalogEntriesRequest request, CatalogWorkspace session)
    {
        CatalogEditResult result = session.RemoveEntries(request.Ids);
        return result.Error == null
            ? Results.Ok(result.Catalog)
            : Results.Json(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    private static IResult MoveEntries(MoveCatalogEntriesRequest request, CatalogWorkspace session)
    {
        CatalogEditResult result = session.MoveEntries(request.Moves);
        return result.Error == null
            ? Results.Ok(result.Catalog)
            : Results.Json(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    private static IResult ApplyEntryDelta(
        ApplyCatalogEntryDeltaRequest request,
        CatalogWorkspace session)
    {
        CatalogEditResult result = session.ApplyEntryDelta(
            request.ExpectedRevision,
            request.Entries,
            request.RemovedIds);
        if (result.Error == null)
        {
            return Results.Ok(result.Catalog);
        }

        int statusCode = result.Error.Code == WorkspaceErrorCodes.CatalogRevisionMismatch
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status422UnprocessableEntity;
        return Results.Json(result.Error, statusCode: statusCode);
    }

    private static IResult ApplyLocales(
        ApplyCatalogLocalesRequest request,
        CatalogWorkspace session)
    {
        CatalogEditResult result = session.ApplyLocales(
            request.ExpectedRevision,
            request.DefaultLocale,
            request.Locales,
            request.Renames,
            request.RemovedIds);
        if (result.Error == null)
        {
            return Results.Ok(result.Catalog);
        }

        int statusCode = result.Error.Code == WorkspaceErrorCodes.CatalogRevisionMismatch
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status422UnprocessableEntity;
        return Results.Json(result.Error, statusCode: statusCode);
    }

    private static IResult Save(SaveCatalogRequest request, CatalogWorkspace session)
    {
        CatalogEditResult result = session.Save(request.OverwriteExternalChanges);
        if (result.Error == null)
        {
            return Results.Ok(result.Catalog);
        }

        int statusCode = result.Error.Code == WorkspaceErrorCodes.CatalogChangedExternally
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status422UnprocessableEntity;
        return Results.Json(result.Error, statusCode: statusCode);
    }

    private static async Task<IResult> RevertAsync(
        CatalogWorkspace session,
        CatalogFileLoader loader,
        CancellationToken cancellationToken)
    {
        string? path = session.GetSnapshot().CatalogPath;
        if (path == null)
        {
            return Results.NotFound(new EditorErrorResponse(
                WorkspaceErrorCodes.CatalogNotOpen,
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
        CatalogWorkspace session,
        CatalogFileLoader loader,
        CancellationToken cancellationToken)
    {
        string? path = session.GetSnapshot().CatalogPath;
        if (path == null)
        {
            return Results.NotFound(new EditorErrorResponse(
                WorkspaceErrorCodes.CatalogNotOpen,
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
