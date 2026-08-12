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
    IReadOnlyList<string> Arguments,
    IReadOnlyList<MessageDiagnosticResponse> Diagnostics);

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
