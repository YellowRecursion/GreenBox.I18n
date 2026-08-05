namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Describes an editor operation failure.
/// </summary>
/// <param name="Code">The stable machine-readable error code.</param>
/// <param name="Message">The human-readable error message.</param>
public sealed record EditorErrorResponse(string Code, string Message);
