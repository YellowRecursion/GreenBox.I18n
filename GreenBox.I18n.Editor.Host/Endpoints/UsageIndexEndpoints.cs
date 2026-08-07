using GreenBox.I18n;
using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Editor.Host.Editor;
using GreenBox.I18n.Editor.Host.Infrastructure;

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
        usageEndpoints.MapGet(string.Empty, ReadSummaryAsync);
        usageEndpoints.MapGet("/entries/{entryId}", ReadEntryAsync);
        return endpoints;
    }

    private static Task<UsageIndexSummaryResponse> ReadSummaryAsync(
        EditorSession session,
        UsageIndexReader reader,
        CancellationToken cancellationToken) =>
        reader.ReadSummaryAsync(session.GetSnapshot().CatalogPath, cancellationToken);

    private static async Task<IResult> ReadEntryAsync(
        string entryId,
        EditorSession session,
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
}
