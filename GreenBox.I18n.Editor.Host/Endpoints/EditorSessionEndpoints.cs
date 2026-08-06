using System.Diagnostics;
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
        sessionEndpoints.MapPost("/open-file", OpenCatalogFile);

        return endpoints;
    }

    private static async Task<IResult> OpenCatalogAsync(
        OpenCatalogRequest request,
        CatalogFileLoader loader,
        EditorSession session,
        EditorPreferencesStore preferences,
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

        EditorSessionResponse response = session.Open(
            loadResult.CatalogPath!,
            loadResult.Catalog!,
            loadResult.ContentHash!);
        try
        {
            preferences.RecordLastCatalog(loadResult.CatalogPath!);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Opening the catalog remains successful when personal state cannot be persisted.
        }

        return Results.Ok(response);
    }

    private static IResult OpenCatalogFile(EditorSession session)
    {
        string? catalogPath = session.GetSnapshot().CatalogPath;
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            return Results.Json(
                new EditorErrorResponse(EditorErrorCodes.CatalogNotOpen, "No catalog is open."),
                statusCode: StatusCodes.Status409Conflict);
        }

        try
        {
            Process.Start(new ProcessStartInfo(catalogPath) { UseShellExecute = true });
            return Results.Ok(new { opened = true });
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return Results.Json(
                new EditorErrorResponse(EditorErrorCodes.CatalogOpenFailed, exception.Message),
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }
}
