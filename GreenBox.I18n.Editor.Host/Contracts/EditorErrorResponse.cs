namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Describes a stable Host or Unity-integration error returned by the local API.
/// </summary>
public sealed record EditorErrorResponse(string Code, string Message);
