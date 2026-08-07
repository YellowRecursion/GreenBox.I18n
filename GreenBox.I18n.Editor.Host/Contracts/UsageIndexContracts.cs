namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Describes the usage index associated with the open catalog.
/// </summary>
public sealed record UsageIndexSummaryResponse(
    string Availability,
    string? Status,
    DateTimeOffset? UpdatedAtUtc,
    string? LastError,
    int FailedSourceCount,
    IReadOnlyList<UsageEntrySummaryResponse> Entries);

/// <summary>
/// Contains usage counts for one entry. Entry IDs are strings because they exceed JavaScript's safe integer range.
/// </summary>
public sealed record UsageEntrySummaryResponse(
    string EntryId,
    int TotalCount,
    int CodeCount,
    int AssetCount);

/// <summary>
/// Contains all indexed locations for one entry.
/// </summary>
public sealed record UsageEntryResponse(
    string Availability,
    string EntryId,
    int TotalCount,
    IReadOnlyList<CodeUsageResponse> Code,
    IReadOnlyList<AssetUsageResponse> Assets);

/// <summary>
/// Describes one C# usage location.
/// </summary>
public sealed record CodeUsageResponse(
    string Assembly,
    string FilePath,
    int Line);

/// <summary>
/// Describes one serialized Unity usage location.
/// </summary>
public sealed record AssetUsageResponse(
    string AssetPath,
    string? AssetGuid,
    string AssetLocalId,
    string? GameObjectLocalId,
    string ObjectPath,
    string ComponentType,
    string PropertyPath,
    int Line,
    bool IsPrefabOverride,
    string? TargetAssetGuid,
    string? TargetLocalId);
