using GreenBox.I18n.Editor.Host.Contracts;
using System.Globalization;
using System.Text.Json;

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
        messageEndpoints.MapPost("/preview", Preview);
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

        return Results.Ok(new MessageAnalysisResponse(
            compilation.IsSuccess,
            arguments,
            ConvertDiagnostics(compilation.Diagnostics)));
    }

    private static IResult Preview(PreviewMessageRequest request)
    {
        if (request.Source == null)
        {
            return BadRequest("message_source_required", "Message source is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Culture))
        {
            return BadRequest("message_culture_required", "A preview culture is required.");
        }

        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(request.Culture);
        }
        catch (CultureNotFoundException)
        {
            return BadRequest(
                "message_culture_invalid",
                $"Culture '{request.Culture}' is not recognized.");
        }

        if (!TryConvertArguments(request.Arguments, out (string Name, object? Value)[] arguments, out string? error))
        {
            return BadRequest("message_argument_invalid", error!);
        }

        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(request.Source);
        if (!compilation.IsSuccess)
        {
            return Results.Ok(new MessagePreviewResponse(
                false,
                string.Empty,
                ConvertDiagnostics(compilation.Diagnostics)));
        }

        I18nMessageFormatResult preview = compilation.Message!.Format(culture, arguments);
        return Results.Ok(new MessagePreviewResponse(
            preview.IsSuccess,
            preview.Text,
            ConvertDiagnostics(preview.Diagnostics)));
    }

    private static bool TryConvertArguments(
        IReadOnlyDictionary<string, JsonElement>? source,
        out (string Name, object? Value)[] arguments,
        out string? error)
    {
        if (source == null || source.Count == 0)
        {
            arguments = Array.Empty<(string, object?)>();
            error = null;
            return true;
        }

        string[] names = source.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        arguments = new (string Name, object? Value)[names.Length];
        for (int index = 0; index < names.Length; index++)
        {
            string name = names[index];
            if (string.IsNullOrWhiteSpace(name))
            {
                error = "Preview argument names cannot be empty.";
                return false;
            }

            JsonElement value = source[name];
            if (!TryConvertArgument(value, out object? converted))
            {
                error = $"Preview argument '{name}' must be a string, number, boolean, or null.";
                return false;
            }

            arguments[index] = (name, converted);
        }

        error = null;
        return true;
    }

    private static bool TryConvertArgument(JsonElement value, out object? converted)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Null:
                converted = null;
                return true;
            case JsonValueKind.String:
                converted = value.GetString();
                return true;
            case JsonValueKind.True:
            case JsonValueKind.False:
                converted = value.GetBoolean();
                return true;
            case JsonValueKind.Number when value.TryGetInt64(out long integer):
                converted = integer;
                return true;
            case JsonValueKind.Number when value.TryGetDecimal(out decimal number):
                converted = number;
                return true;
            case JsonValueKind.Number:
                converted = value.GetDouble();
                return true;
            default:
                converted = null;
                return false;
        }
    }

    private static MessageDiagnosticResponse[] ConvertDiagnostics(
        IReadOnlyList<I18nMessageDiagnostic> diagnostics)
    {
        return diagnostics
            .Select(diagnostic => new MessageDiagnosticResponse(
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.Position,
                diagnostic.ArgumentName))
            .ToArray();
    }

    private static IResult BadRequest(string code, string message)
    {
        return Results.BadRequest(new EditorErrorResponse(code, message));
    }
}
