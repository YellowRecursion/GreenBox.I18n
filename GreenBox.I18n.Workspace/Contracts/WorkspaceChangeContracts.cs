namespace GreenBox.I18n.Workspace.Contracts;

public sealed record PrepareEntryChangesRequest(
    long ExpectedRevision,
    IReadOnlyList<WorkspaceEntryMutation> Changes,
    IReadOnlyDictionary<string, int?>? UsageCounts = null,
    bool AllowDeleteWithUsages = false,
    string? UsageRevision = null);

public sealed record WorkspaceEntryMutation(
    string Operation,
    string? Id = null,
    string? Path = null,
    bool SetComment = false,
    string? Comment = null,
    IReadOnlyDictionary<string, WorkspaceLocaleValuePatch>? Locales = null);

public sealed record WorkspaceLocaleValuePatch(
    bool SetText = false,
    string? Text = null,
    bool SetAsset = false,
    CatalogAssetReferenceEditRequest? Asset = null);

public sealed record PrepareLocaleChangesRequest(
    long ExpectedRevision,
    string DefaultLocale,
    IReadOnlyList<CatalogLocaleEditRequest> Locales,
    IReadOnlyList<CatalogLocaleRenameRequest> Renames,
    IReadOnlyList<string> RemovedIds);

public sealed record WorkspaceChangeSetResponse(
    string ChangeSetId,
    long BaseRevision,
    DateTimeOffset ExpiresAtUtc,
    bool CanApply,
    WorkspaceChangeSummaryResponse Summary,
    IReadOnlyList<WorkspaceEntryChangePreview> Changes,
    IReadOnlyList<WorkspaceChangeNotice> Warnings,
    IReadOnlyList<WorkspaceChangeNotice> Blockers,
    IReadOnlyList<CatalogDiagnosticResponse> Diagnostics);

public sealed record WorkspaceChangeSummaryResponse(
    int CreatedEntries,
    int UpdatedEntries,
    int DeletedEntries,
    int ChangedLocales);

public sealed record WorkspaceEntryChangePreview(
    string Operation,
    string Id,
    string? BeforePath,
    string? AfterPath);

public sealed record WorkspaceChangeNotice(string Code, string Message, string? EntryId = null);

public sealed record ApplyWorkspaceChangeSetRequest(string ChangeSetId);

public sealed record ApplyWorkspaceChangeSetResponse(
    string ChangeSetId,
    long Revision,
    string CatalogPath,
    bool Saved);
