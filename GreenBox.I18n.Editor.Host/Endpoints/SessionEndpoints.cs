using System.Diagnostics;
using System.Runtime.InteropServices;
using GreenBox.I18n.Editor.Host.Editor;
using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Editor.Host.Infrastructure;
using GreenBox.I18n.Workspace;
using GreenBox.I18n.Workspace.Contracts;

namespace GreenBox.I18n.Editor.Host.Endpoints;

/// <summary>
/// Registers endpoints related to the editor session.
/// </summary>
public static class SessionEndpoints
{
    /// <summary>
    /// Maps editor session endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The supplied endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder sessionEndpoints = endpoints.MapGroup("/api/session");

        sessionEndpoints.MapGet(string.Empty, (CatalogWorkspace session) => session.GetSnapshot());
        sessionEndpoints.MapPost("/open", OpenCatalogAsync);
        sessionEndpoints.MapPost("/pick-file", PickCatalogFileAsync);
        sessionEndpoints.MapPost("/open-file", OpenCatalogFile);

        return endpoints;
    }

    private static async Task<IResult> OpenCatalogAsync(
        OpenCatalogRequest request,
        CatalogFileLoader loader,
        CatalogWorkspace session,
        EditorPreferencesStore preferences,
        CancellationToken cancellationToken)
    {
        CatalogLoadResult loadResult = await loader.LoadAsync(request.Path, cancellationToken);
        if (!loadResult.IsSuccess)
        {
            int statusCode = loadResult.Error!.Code == WorkspaceErrorCodes.CatalogNotFound
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status422UnprocessableEntity;

            return Results.Json(loadResult.Error, statusCode: statusCode);
        }

        WorkspaceResponse response = session.Open(
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

    private static async Task<IResult> PickCatalogFileAsync(CatalogFilePicker picker)
    {
        try
        {
            string? path = await picker.PickAsync();
            return Results.Ok(new PickCatalogFileResponse(path));
        }
        catch (Exception exception) when (
            exception is PlatformNotSupportedException or COMException)
        {
            return Results.Json(
                new EditorErrorResponse(HostErrorCodes.CatalogOpenFailed, exception.Message),
                statusCode: StatusCodes.Status501NotImplemented);
        }
    }

    private static IResult OpenCatalogFile(CatalogWorkspace session)
    {
        string? catalogPath = session.GetSnapshot().CatalogPath;
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            return Results.Json(
                new EditorErrorResponse(WorkspaceErrorCodes.CatalogNotOpen, "No catalog is open."),
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
                new EditorErrorResponse(HostErrorCodes.CatalogOpenFailed, exception.Message),
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }
}
