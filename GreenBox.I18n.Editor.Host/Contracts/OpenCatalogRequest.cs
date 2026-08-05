namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Requests that a catalog file becomes the editor working copy.
/// </summary>
/// <param name="Path">The path of the catalog JSON file.</param>
public sealed record OpenCatalogRequest(string Path);
