namespace GreenBox.I18n.Workspace.Contracts;

/// <summary>
/// Describes an editor operation failure.
/// </summary>
/// <param name="Code">The stable machine-readable error code.</param>
/// <param name="Message">The human-readable error message.</param>
public sealed record WorkspaceErrorResponse(string Code, string Message);
