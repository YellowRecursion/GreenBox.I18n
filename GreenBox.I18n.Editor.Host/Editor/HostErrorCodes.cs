namespace GreenBox.I18n.Editor.Host.Editor;

/// <summary>
/// Contains stable machine-readable codes produced by Host and Unity integrations.
/// </summary>
public static class HostErrorCodes
{
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

    /// <summary>Indicates that the requested usage location is not in the current index.</summary>
    public const string UsageLocationNotFound = "usage_location_not_found";

    /// <summary>Indicates that the Unity Editor process associated with the project is offline.</summary>
    public const string UnityEditorOffline = "unity_editor_offline";

    /// <summary>Indicates that Unity did not answer a project bridge command in time.</summary>
    public const string UnityEditorCommandTimeout = "unity_editor_command_timeout";

    /// <summary>Indicates that a Unity bridge command could not be delivered.</summary>
    public const string UnityEditorCommandFailed = "unity_editor_command_failed";
}
