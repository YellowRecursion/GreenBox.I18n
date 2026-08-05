namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Describes the state exposed to an editor client.
/// </summary>
/// <param name="HasCatalog">Whether a localization catalog is loaded.</param>
/// <param name="Revision">The revision of the server-side working copy.</param>
public sealed record EditorSessionResponse(bool HasCatalog, long Revision);
