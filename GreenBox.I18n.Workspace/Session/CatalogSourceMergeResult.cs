using GreenBox.I18n.Workspace.Contracts;

namespace GreenBox.I18n.Workspace;

/// <summary>
/// Represents the result of merging a source catalog into the workspace working copy.
/// </summary>
public sealed class CatalogSourceMergeResult
{
    private CatalogSourceMergeResult(
        CatalogResponse? catalog,
        WorkspaceErrorResponse? error,
        IReadOnlyList<I18nCatalogMergeConflict> conflicts)
    {
        Catalog = catalog;
        Error = error;
        Conflicts = conflicts;
    }

    /// <summary>Gets the updated catalog snapshot after a successful merge.</summary>
    public CatalogResponse? Catalog { get; }

    /// <summary>Gets the operation error when the merge failed.</summary>
    public WorkspaceErrorResponse? Error { get; }

    /// <summary>Gets the field-level conflicts that prevented the merge.</summary>
    public IReadOnlyList<I18nCatalogMergeConflict> Conflicts { get; }

    /// <summary>Creates a successful merge result.</summary>
    public static CatalogSourceMergeResult Success(CatalogResponse catalog)
    {
        return new CatalogSourceMergeResult(catalog, null, Array.Empty<I18nCatalogMergeConflict>());
    }

    /// <summary>Creates a failed merge result.</summary>
    public static CatalogSourceMergeResult Failure(IReadOnlyList<I18nCatalogMergeConflict> conflicts)
    {
        return new CatalogSourceMergeResult(
            null,
            new WorkspaceErrorResponse(
                WorkspaceErrorCodes.CatalogMergeConflict,
                $"Automatic merge found {conflicts.Count} conflicting " +
                (conflicts.Count == 1 ? "field." : "fields.")),
            conflicts);
    }
}
