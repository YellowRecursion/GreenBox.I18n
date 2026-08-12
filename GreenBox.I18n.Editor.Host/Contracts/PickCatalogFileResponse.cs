namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Describes a catalog selected by the native file picker.
/// </summary>
/// <param name="Path">The absolute catalog path, or <see langword="null"/> when cancelled.</param>
public sealed record PickCatalogFileResponse(string? Path);
