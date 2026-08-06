namespace GreenBox.I18n.Editor.Host.Editor;

/// <summary>
/// Contains stable machine-readable codes produced by editor operations.
/// </summary>
public static class EditorErrorCodes
{
    /// <summary>Indicates that no catalog path was supplied.</summary>
    public const string MissingCatalogPath = "missing_catalog_path";

    /// <summary>Indicates that a supplied catalog path is invalid.</summary>
    public const string InvalidCatalogPath = "invalid_catalog_path";

    /// <summary>Indicates that the catalog file does not exist.</summary>
    public const string CatalogNotFound = "catalog_not_found";

    /// <summary>Indicates that the catalog file could not be read.</summary>
    public const string CatalogReadFailed = "catalog_read_failed";

    /// <summary>Indicates that the catalog JSON could not be deserialized.</summary>
    public const string InvalidCatalogJson = "invalid_catalog_json";

    /// <summary>Indicates that the deserialized catalog failed validation.</summary>
    public const string InvalidCatalog = "invalid_catalog";

    /// <summary>Indicates that the editor session has no open catalog.</summary>
    public const string CatalogNotOpen = "catalog_not_open";

    /// <summary>Indicates that an edit was based on an outdated working-copy revision.</summary>
    public const string CatalogRevisionMismatch = "catalog_revision_mismatch";

    /// <summary>Indicates that the catalog file changed after it was loaded.</summary>
    public const string CatalogChangedExternally = "catalog_changed_externally";

    /// <summary>The catalog contains external changes that cannot be merged safely.</summary>
    public const string CatalogMergeConflict = "catalog_merge_conflict";

    /// <summary>Indicates that the catalog file could not be written.</summary>
    public const string CatalogWriteFailed = "catalog_write_failed";

    /// <summary>Indicates that the operating system could not open the catalog file.</summary>
    public const string CatalogOpenFailed = "catalog_open_failed";

    /// <summary>Indicates that personal editor preferences could not be written.</summary>
    public const string PreferencesWriteFailed = "preferences_write_failed";

    /// <summary>Indicates that the open catalog is not located inside a Unity project.</summary>
    public const string UnityProjectNotFound = "unity_project_not_found";

    /// <summary>Indicates that a Unity asset GUID cannot be resolved in the current project.</summary>
    public const string UnityAssetNotFound = "unity_asset_not_found";

    /// <summary>Indicates that Unity has not published a compatible active selection.</summary>
    public const string UnitySelectionUnavailable = "unity_selection_unavailable";

    /// <summary>Indicates that a dropped file does not match Unity's active selection.</summary>
    public const string UnitySelectionMismatch = "unity_selection_mismatch";

    /// <summary>Indicates that the operating system could not open a Unity asset.</summary>
    public const string UnityAssetOpenFailed = "unity_asset_open_failed";
}
