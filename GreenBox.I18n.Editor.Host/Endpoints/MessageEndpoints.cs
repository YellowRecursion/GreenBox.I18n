using GreenBox.I18n.Editor.Host.Contracts;

namespace GreenBox.I18n.Editor.Host.Endpoints;

/// <summary>
/// Registers stateless MessageFormat authoring endpoints.
/// </summary>
public static class MessageEndpoints
{
    /// <summary>
    /// Maps MessageFormat authoring endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The supplied endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapMessageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder messageEndpoints = endpoints.MapGroup("/api/messages");
        messageEndpoints.MapPost("/analyze", Analyze);
        return endpoints;
    }

    private static IResult Analyze(AnalyzeMessageRequest request)
    {
        if (request.Source == null)
        {
            return Results.BadRequest(new EditorErrorResponse(
                "message_source_required",
                "Message source is required."));
        }

        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(request.Source);
        IReadOnlyList<string> arguments = compilation.Message?.ArgumentNames ?? Array.Empty<string>();
        MessageDiagnosticResponse[] diagnostics = compilation.Diagnostics
            .Select(diagnostic => new MessageDiagnosticResponse(
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.Position,
                diagnostic.ArgumentName))
            .ToArray();

        return Results.Ok(new MessageAnalysisResponse(
            compilation.IsSuccess,
            arguments,
            diagnostics));
    }
}
