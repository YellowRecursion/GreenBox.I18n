namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Describes a catalog merge that requires explicit conflict resolution.
/// </summary>
/// <param name="Code">The stable machine-readable error code.</param>
/// <param name="Message">The human-readable merge summary.</param>
/// <param name="Conflicts">The conflicting catalog fields.</param>
public sealed record CatalogMergeErrorResponse(
    string Code,
    string Message,
    IReadOnlyList<CatalogMergeConflictResponse> Conflicts);

/// <summary>
/// Describes one catalog field that could not be merged safely.
/// </summary>
/// <param name="JsonPath">The logical JSON path of the conflicting field.</param>
/// <param name="Message">The human-readable conflict description.</param>
public sealed record CatalogMergeConflictResponse(string JsonPath, string Message);
