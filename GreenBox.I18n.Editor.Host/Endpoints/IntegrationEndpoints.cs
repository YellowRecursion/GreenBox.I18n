using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Editor.Host.Infrastructure;

namespace GreenBox.I18n.Editor.Host.Endpoints;

/// <summary>
/// Registers local development-tool integration endpoints.
/// </summary>
public static class IntegrationEndpoints
{
    /// <summary>
    /// Maps integration status and explicit setup actions.
    /// </summary>
    public static IEndpointRouteBuilder MapIntegrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder integrations = endpoints.MapGroup("/api/integrations");
        integrations.MapGet("/codex", GetCodexStatusAsync);
        integrations.MapPost("/codex/connect", ConnectCodexAsync);
        return endpoints;
    }

    private static Task<CodexIntegrationStatusResponse> GetCodexStatusAsync(
        CodexMcpIntegrationService service,
        CancellationToken cancellationToken) =>
        service.GetStatusAsync(cancellationToken);

    private static async Task<IResult> ConnectCodexAsync(
        ConfigureCodexIntegrationRequest request,
        CodexMcpIntegrationService service,
        CancellationToken cancellationToken)
    {
        if (!request.Confirm)
        {
            return Results.BadRequest(new { message = "Explicit confirmation is required." });
        }

        CodexIntegrationStatusResponse result = await service.ConfigureAsync(cancellationToken);
        return result.Status == "error"
            ? Results.Json(result, statusCode: StatusCodes.Status422UnprocessableEntity)
            : Results.Ok(result);
    }
}
