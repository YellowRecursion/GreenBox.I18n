using GreenBox.I18n.Workspace.Contracts;

namespace GreenBox.I18n.Mcp;

public sealed record McpPrepareEntryChangesRequest(
    long ExpectedRevision,
    IReadOnlyList<WorkspaceEntryMutation> Changes,
    bool AllowDeleteWithUsages = false);

public sealed record McpWorkspaceIssuesRequest(
    IReadOnlyList<string>? Kinds = null,
    string? Cursor = null,
    int? Limit = null);

public sealed record McpWorkspaceIssuesResponse(
    long Revision,
    string UsageAvailability,
    string? UsageIndexStatus,
    int TotalCount,
    IReadOnlyList<McpWorkspaceIssue> Issues,
    string? NextCursor);

public sealed record McpWorkspaceIssue(
    string Kind,
    string Code,
    string Severity,
    string Message,
    string? EntryId,
    string? EntryPath,
    string? LocaleId,
    int? UsageCount);

public sealed record McpUsageEntryResponse(
    string Availability,
    string EntryId,
    int TotalCount,
    IReadOnlyList<McpCodeUsage> Code,
    IReadOnlyList<McpAssetUsage> Assets);

public sealed record McpCodeUsage(string LocationId, string Assembly, string FilePath, int Line);

public sealed record McpAssetUsage(
    string LocationId,
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

public sealed record McpOpenUsageResponse(string Status, string? Message);

public sealed record McpMessageAnalysisResponse(
    bool IsValid,
    IReadOnlyList<McpMessageArgument> Arguments,
    IReadOnlyList<McpMessageDiagnostic> Diagnostics);

public sealed record McpMessageArgument(string Name, string Kind);

public sealed record McpMessageDiagnostic(string Code, string Message, int Position, string? ArgumentName);

public sealed record McpMessagePreviewResponse(
    bool IsSuccess,
    string Text,
    IReadOnlyList<McpMessageDiagnostic> Diagnostics);

internal sealed record McpHostError(string Code, string Message);
