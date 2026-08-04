using System.Text.Json;
using System.Text.Json.Serialization;

namespace GreenBox.I18n.Cli;

internal sealed class ValidationReport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public required string File { get; init; }

    public required bool IsValid { get; init; }

    public required int ErrorCount { get; init; }

    public required int WarningCount { get; init; }

    public required IReadOnlyList<ValidationDiagnosticReport> Diagnostics { get; init; }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, JsonOptions);
    }
}

internal sealed class ValidationDiagnosticReport
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string JsonPath { get; init; }

    public required string Message { get; init; }

    public int? Line { get; init; }

    public int? Position { get; init; }
}
