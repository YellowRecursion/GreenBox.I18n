namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Requests analysis of an unsaved localized message draft.
/// </summary>
/// <param name="Source">The MessageFormat 2 source to analyze.</param>
public sealed record AnalyzeMessageRequest(string? Source);

/// <summary>
/// Describes the result of analyzing one localized message draft.
/// </summary>
/// <param name="IsValid">Whether the source can be compiled.</param>
/// <param name="Arguments">The external argument names used by the message.</param>
/// <param name="Diagnostics">The problems found in the source.</param>
public sealed record MessageAnalysisResponse(
    bool IsValid,
    IReadOnlyList<MessageArgumentResponse> Arguments,
    IReadOnlyList<MessageDiagnosticResponse> Diagnostics);

/// <summary>
/// Describes one external argument required by a compiled message.
/// </summary>
/// <param name="Name">The argument name used by callers.</param>
/// <param name="Kind">The stable preview value kind: unspecified, string, or number.</param>
public sealed record MessageArgumentResponse(string Name, string Kind);

/// <summary>
/// Describes one problem in a localized message draft.
/// </summary>
/// <param name="Code">The stable machine-readable diagnostic code.</param>
/// <param name="Message">The human-readable explanation.</param>
/// <param name="Position">The zero-based source position, or -1 when unavailable.</param>
/// <param name="ArgumentName">The affected external argument, when applicable.</param>
public sealed record MessageDiagnosticResponse(
    string Code,
    string Message,
    int Position,
    string? ArgumentName);

/// <summary>
/// Requests formatting of an unsaved localized message draft with preview values.
/// </summary>
/// <param name="Source">The MessageFormat 2 source to format.</param>
/// <param name="Culture">The .NET culture used for locale-aware formatting and selection.</param>
/// <param name="Arguments">The primitive preview values indexed by external argument name.</param>
public sealed record PreviewMessageRequest(
    string? Source,
    string? Culture,
    IReadOnlyDictionary<string, System.Text.Json.JsonElement>? Arguments);

/// <summary>
/// Describes the formatted preview and any non-throwing diagnostics.
/// </summary>
/// <param name="IsSuccess">Whether compilation and formatting completed without diagnostics.</param>
/// <param name="Text">The formatted output or readable diagnostic fallback.</param>
/// <param name="Diagnostics">The problems found while compiling or formatting.</param>
public sealed record MessagePreviewResponse(
    bool IsSuccess,
    string Text,
    IReadOnlyList<MessageDiagnosticResponse> Diagnostics);
