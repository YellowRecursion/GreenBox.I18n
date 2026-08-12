namespace GreenBox.I18n.Workspace;

/// <summary>
/// Stable machine-readable failures produced by the catalog workspace.
/// </summary>
public static class WorkspaceErrorCodes
{
    public const string MissingCatalogPath = "missing_catalog_path";
    public const string InvalidCatalogPath = "invalid_catalog_path";
    public const string CatalogNotFound = "catalog_not_found";
    public const string CatalogReadFailed = "catalog_read_failed";
    public const string InvalidCatalogJson = "invalid_catalog_json";
    public const string InvalidCatalog = "invalid_catalog";
    public const string CatalogNotOpen = "catalog_not_open";
    public const string CatalogRevisionMismatch = "catalog_revision_mismatch";
    public const string CatalogChangedExternally = "catalog_changed_externally";
    public const string CatalogMergeConflict = "catalog_merge_conflict";
    public const string CatalogWriteFailed = "catalog_write_failed";
    public const string InvalidCursor = "invalid_cursor";
    public const string InvalidRequest = "invalid_request";
    public const string WorkspaceHasUnsavedChanges = "workspace_has_unsaved_changes";
    public const string ChangeSetNotFound = "change_set_not_found";
    public const string ChangeSetExpired = "change_set_expired";
    public const string ChangeSetBlocked = "change_set_blocked";
    public const string UsageIndexChanged = "usage_index_changed";
}
