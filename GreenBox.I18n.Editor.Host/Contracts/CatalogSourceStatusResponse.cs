namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Describes whether the catalog source file still matches the editor baseline.
/// </summary>
/// <param name="HasChanged">Whether the source file changed after it was loaded or saved.</param>
/// <param name="IsAvailable">Whether the source file could be read.</param>
/// <param name="Message">The source access error, if one occurred.</param>
public sealed record CatalogSourceStatusResponse(
    bool HasChanged,
    bool IsAvailable,
    string? Message);
