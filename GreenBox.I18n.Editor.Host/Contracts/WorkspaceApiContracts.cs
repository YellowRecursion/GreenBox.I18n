using GreenBox.I18n.Workspace.Contracts;

namespace GreenBox.I18n.Editor.Host.Contracts;

public sealed record OpenWorkspaceRequest(string Path);

public sealed record PrepareWorkspaceEntryChangesRequest(
    long ExpectedRevision,
    IReadOnlyList<WorkspaceEntryMutation> Changes,
    bool AllowDeleteWithUsages = false);

public sealed record WorkspaceIssuesRequest(
    IReadOnlyList<string>? Kinds = null,
    string? Cursor = null,
    int? Limit = null);

public sealed record WorkspaceIssuesResponse(
    long Revision,
    string UsageAvailability,
    string? UsageIndexStatus,
    int TotalCount,
    IReadOnlyList<WorkspaceIssueResponse> Issues,
    string? NextCursor);

public sealed record WorkspaceIssueResponse(
    string Kind,
    string Code,
    string Severity,
    string Message,
    string? EntryId = null,
    string? EntryPath = null,
    string? LocaleId = null,
    int? UsageCount = null);
