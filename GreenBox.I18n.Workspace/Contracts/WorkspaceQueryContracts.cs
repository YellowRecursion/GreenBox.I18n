namespace GreenBox.I18n.Workspace.Contracts;

public sealed record WorkspaceContextResponse(
    bool HasCatalog,
    long Revision,
    string? CatalogPath,
    string? DefaultLocale,
    IReadOnlyList<CatalogLocaleResponse> Locales,
    int EntryCount,
    bool HasUnsavedChanges,
    CatalogSourceStatusResponse SourceStatus);

public sealed record WorkspaceEntrySearchRequest(
    string? Query = null,
    string? PathPrefix = null,
    IReadOnlyList<string>? LocaleIds = null,
    string? MissingLocale = null,
    bool? HasText = null,
    bool? HasAsset = null,
    bool IncludeLocalePreviews = true,
    string? Cursor = null,
    int? Limit = null);

public sealed record WorkspaceEntrySearchResponse(
    long Revision,
    IReadOnlyList<WorkspaceEntrySummaryResponse> Entries,
    string? NextCursor);

public sealed record WorkspaceEntrySummaryResponse(
    string Id,
    string Path,
    string? Comment,
    IReadOnlyDictionary<string, WorkspaceTextPreviewResponse> LocalePreviews,
    bool HasText,
    bool HasAsset,
    IReadOnlyList<string> MissingLocales);

public sealed record WorkspaceTextPreviewResponse(string Text, bool Truncated);

public sealed record WorkspaceEntriesResponse(
    long Revision,
    IReadOnlyList<CatalogEntryResponse> Entries,
    IReadOnlyList<string> MissingIds);

public sealed record WorkspaceWorkingChangesResponse(
    long Revision,
    bool HasChanges,
    IReadOnlyList<string> DirtyLocaleIds,
    IReadOnlyList<string> DirtyEntryIds,
    IReadOnlyList<string> DirtyPaths,
    CatalogSourceStatusResponse SourceStatus);
