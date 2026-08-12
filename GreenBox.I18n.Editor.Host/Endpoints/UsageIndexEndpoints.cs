using GreenBox.I18n;
using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Editor.Host.Editor;
using GreenBox.I18n.Editor.Host.Infrastructure;
using GreenBox.I18n.Workspace;

namespace GreenBox.I18n.Editor.Host.Endpoints;

/// <summary>
/// Registers read-only endpoints for Unity usage-index data.
/// </summary>
public static class UsageIndexEndpoints
{
    /// <summary>
    /// Maps usage-index endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapUsageIndexEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder usageEndpoints = endpoints.MapGroup("/api/usage-index");
        usageEndpoints.MapGet("/state", ReadStateAsync);
        usageEndpoints.MapGet(string.Empty, ReadSummaryAsync);
        usageEndpoints.MapGet("/entries/{entryId}", ReadEntryAsync);
        usageEndpoints.MapPost("/open", OpenUsageAsync);
        return endpoints;
    }

    private static Task<UsageIndexSummaryResponse> ReadSummaryAsync(
        CatalogWorkspace session,
        UsageIndexReader reader,
        CancellationToken cancellationToken) =>
        reader.ReadSummaryAsync(session.GetSnapshot().CatalogPath, cancellationToken);

    private static Task<UsageIndexStateResponse> ReadStateAsync(
        CatalogWorkspace session,
        UsageIndexReader reader,
        CancellationToken cancellationToken) =>
        reader.ReadStateAsync(session.GetSnapshot().CatalogPath, cancellationToken);

    private static async Task<IResult> ReadEntryAsync(
        string entryId,
        CatalogWorkspace session,
        UsageIndexReader reader,
        CancellationToken cancellationToken)
    {
        if (!I18nEntryId.TryParse(entryId, out long numericEntryId))
        {
            return Results.BadRequest(new EditorErrorResponse(
                I18nEditCodes.InvalidId,
                $"Entry ID does not use the GreenBox I18n ID format: '{entryId}'."));
        }

        UsageEntryResponse response = await reader.ReadEntryAsync(
            session.GetSnapshot().CatalogPath,
            numericEntryId,
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> OpenUsageAsync(
        OpenUsageRequest request,
        CatalogWorkspace session,
        UsageNavigationService navigation,
        CancellationToken cancellationToken)
    {
        if (!I18nEntryId.TryParse(request.EntryId, out long entryId))
        {
            return Results.BadRequest(new EditorErrorResponse(
                I18nEditCodes.InvalidId,
                $"Entry ID does not use the GreenBox I18n ID format: '{request.EntryId}'."));
        }

        UsageNavigationResult result = await navigation.OpenAsync(
            session.GetSnapshot().CatalogPath,
            entryId,
            request.LocationId,
            cancellationToken);
        if (result.Response != null)
        {
            return Results.Ok(result.Response);
        }

        int statusCode = result.Error!.Code switch
        {
            HostErrorCodes.UsageLocationNotFound => StatusCodes.Status404NotFound,
            HostErrorCodes.UnityEditorOffline or HostErrorCodes.UnityProjectNotFound =>
                StatusCodes.Status409Conflict,
            HostErrorCodes.UnityEditorCommandTimeout => StatusCodes.Status504GatewayTimeout,
            _ => StatusCodes.Status422UnprocessableEntity,
        };
        return Results.Json(result.Error, statusCode: statusCode);
    }
}
