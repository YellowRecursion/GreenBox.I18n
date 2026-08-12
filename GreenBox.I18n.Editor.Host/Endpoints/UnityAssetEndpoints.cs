using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Editor.Host.Editor;
using GreenBox.I18n.Editor.Host.Infrastructure;
using GreenBox.I18n.Workspace;

namespace GreenBox.I18n.Editor.Host.Endpoints;

/// <summary>
/// Registers endpoints used by Unity asset-reference controls.
/// </summary>
public static class UnityAssetEndpoints
{
    /// <summary>
    /// Maps Unity asset-reference endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The supplied endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapUnityAssetEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder assetEndpoints = endpoints.MapGroup("/api/unity-assets");
        assetEndpoints.MapGet("/{assetGuid}", Resolve);
        assetEndpoints.MapPost("/resolve-drop", ResolveDrop);
        assetEndpoints.MapPost("/open", Open);
        return endpoints;
    }

    private static IResult Resolve(
        string assetGuid,
        CatalogWorkspace session,
        UnityAssetReferenceService service)
    {
        UnityAssetReferenceResult result = service.Resolve(session.GetSnapshot().CatalogPath, assetGuid);
        return ToResult(result);
    }

    private static IResult ResolveDrop(
        ResolveDroppedUnityAssetRequest request,
        CatalogWorkspace session,
        UnityAssetReferenceService service)
    {
        UnityAssetReferenceResult result = service.ResolveDrop(
            session.GetSnapshot().CatalogPath,
            request.FileName);
        return ToResult(result);
    }

    private static IResult Open(
        OpenUnityAssetRequest request,
        CatalogWorkspace session,
        UnityAssetReferenceService service)
    {
        EditorErrorResponse? error = service.Open(session.GetSnapshot().CatalogPath, request.AssetGuid);
        return error == null
            ? Results.Ok(new { opened = true })
            : Results.Json(error, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    private static IResult ToResult(UnityAssetReferenceResult result)
    {
        return result.Reference == null
            ? Results.Json(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity)
            : Results.Ok(result.Reference);
    }
}
