namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Requests resolution of Unity's active Project window selection for a dropped file.
/// </summary>
/// <param name="FileName">The browser-visible name of the dropped source file.</param>
public sealed record ResolveDroppedUnityAssetRequest(string FileName);

/// <summary>
/// Requests opening a referenced Unity asset with its operating-system application.
/// </summary>
/// <param name="AssetGuid">The Unity asset GUID to open.</param>
public sealed record OpenUnityAssetRequest(string AssetGuid);

/// <summary>
/// Describes a resolved Unity asset reference without persisting its path in the catalog.
/// </summary>
/// <param name="AssetGuid">The authoritative Unity asset GUID.</param>
/// <param name="LocalFileId">The optional local identifier of an object inside the asset file.</param>
/// <param name="AssetPath">The current project-relative Unity asset path.</param>
/// <param name="FileName">The source asset file name.</param>
/// <param name="ObjectName">The selected Unity object name when Unity supplied it.</param>
public sealed record UnityAssetReferenceResponse(
    string AssetGuid,
    string? LocalFileId,
    string AssetPath,
    string FileName,
    string? ObjectName);
