using System.ComponentModel;
using GreenBox.I18n.Workspace.Contracts;
using ModelContextProtocol.Server;

namespace GreenBox.I18n.Mcp;

[McpServerToolType]
public sealed class CatalogTools
{
    private readonly GreenBoxHostClient _host;

    public CatalogTools(GreenBoxHostClient host)
    {
        _host = host;
    }

    [McpServerTool(Name = "search_entries", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Searches the active catalog without loading it all. Results are deterministic and paged; pass nextCursor back unchanged.")]
    public Task<McpToolResponse<WorkspaceEntrySearchResponse>> SearchEntries(
        [Description("Text matched against ID, path, comment, and localized texts.")] string? query = null,
        [Description("Optional case-insensitive path prefix, for example UI.Menu.")] string? pathPrefix = null,
        [Description("Limit text search and previews to these locale IDs.")] IReadOnlyList<string>? localeIds = null,
        [Description("Return only entries with no text or asset for this locale.")] string? missingLocale = null,
        [Description("Filter by whether any selected locale contains text.")] bool? hasText = null,
        [Description("Filter by whether any selected locale contains an asset.")] bool? hasAsset = null,
        [Description("Include localized text previews. Disable for metadata-only scans.")] bool includeLocalePreviews = true,
        [Description("Opaque cursor returned by the previous page.")] string? cursor = null,
        [Description("Page size from 1 to 200; defaults to 50.")] int? limit = null,
        CancellationToken cancellationToken = default) =>
        _host.SafeAsync(() => _host.SearchEntriesAsync(
            new WorkspaceEntrySearchRequest(
                query,
                pathPrefix,
                localeIds,
                missingLocale,
                hasText,
                hasAsset,
                includeLocalePreviews,
                cursor,
                limit),
            cancellationToken));

    [McpServerTool(Name = "get_entries", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns complete values for up to 50 entries by stable ID. Keep IDs as decimal strings; use the text resource for very large individual texts.")]
    public Task<McpToolResponse<WorkspaceEntriesResponse>> GetEntries(
        [Description("Stable GreenBox entry IDs as decimal strings.")] IReadOnlyList<string> entryIds,
        CancellationToken cancellationToken) =>
        _host.SafeAsync(() => _host.GetEntriesAsync(entryIds, cancellationToken));

    [McpServerTool(Name = "get_catalog_issues", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns paged validation, incomplete-localization, missing-locale, unused-entry, and dangling-usage issues. Unused entries are reported only when Unity usage data is reliable.")]
    public Task<McpToolResponse<McpWorkspaceIssuesResponse>> GetCatalogIssues(
        [Description("Optional kinds: validation, incomplete, missingLocale, unused, danglingUsage. Use incomplete for the same per-text and per-asset completeness warnings shown by the Web editor. Omit for all.")] IReadOnlyList<string>? kinds = null,
        [Description("Opaque cursor returned by the previous page.")] string? cursor = null,
        [Description("Page size from 1 to 500; defaults to 100.")] int? limit = null,
        CancellationToken cancellationToken = default) =>
        _host.SafeAsync(() => _host.GetIssuesAsync(
            new McpWorkspaceIssuesRequest(kinds, cursor, limit),
            cancellationToken));

    [McpServerTool(Name = "get_entry_usages", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns indexed C# and Unity asset locations for one entry. Availability says whether a zero count is trustworthy.")]
    public Task<McpToolResponse<McpUsageEntryResponse>> GetEntryUsages(
        [Description("Stable GreenBox entry ID as a decimal string.")] string entryId,
        CancellationToken cancellationToken) =>
        _host.SafeAsync(() => _host.GetEntryUsagesAsync(entryId, cancellationToken));

    [McpServerTool(Name = "open_location", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = true, UseStructuredContent = true)]
    [Description("Asks the matching Unity Editor to open a code or asset location returned by get_entry_usages.")]
    public Task<McpToolResponse<McpOpenUsageResponse>> OpenLocation(
        [Description("Entry ID associated with the location.")] string entryId,
        [Description("Opaque locationId returned by get_entry_usages.")] string locationId,
        CancellationToken cancellationToken) =>
        _host.SafeAsync(() => _host.OpenLocationAsync(entryId, locationId, cancellationToken));
}
