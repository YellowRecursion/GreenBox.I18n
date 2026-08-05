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
}
