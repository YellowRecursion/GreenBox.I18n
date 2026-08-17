using System.Globalization;
using System.Text;
using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Editor.Host.Infrastructure;
using GreenBox.I18n.Workspace;
using GreenBox.I18n.Workspace.Contracts;

namespace GreenBox.I18n.Editor.Host.Endpoints;

/// <summary>
/// Exposes the shared catalog workspace to trusted local adapters such as MCP.
/// </summary>
public static class WorkspaceEndpoints
{
    private const int DefaultIssuePageSize = 100;
    private const int MaximumIssuePageSize = 500;

    public static IEndpointRouteBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder workspace = endpoints.MapGroup("/api/workspace");
        workspace.MapGet(string.Empty, (CatalogWorkspace state) => state.GetContext());
        workspace.MapPost("/open", OpenAsync);
        workspace.MapPost("/entries/search", SearchEntries);
        workspace.MapPost("/entries/get", GetEntries);
        workspace.MapGet("/changes", (CatalogWorkspace state) => Execute(state.GetWorkingChanges));
        workspace.MapPost("/changes/prepare-entries", PrepareEntryChangesAsync);
        workspace.MapPost("/changes/prepare-locales", PrepareLocaleChanges);
        workspace.MapPost("/changes/apply", ApplyChangeSetAsync);
        workspace.MapPost("/issues", ReadIssuesAsync);
        return endpoints;
    }

    private static async Task<IResult> OpenAsync(
        OpenWorkspaceRequest request,
        CatalogFileLoader loader,
        CatalogWorkspace workspace,
        EditorPreferencesStore preferences,
        CancellationToken cancellationToken)
    {
        if (workspace.GetContext().HasUnsavedChanges)
        {
            return WorkspaceError(new WorkspaceErrorResponse(
                WorkspaceErrorCodes.WorkspaceHasUnsavedChanges,
                "The Web editor has unsaved changes. Save or revert them before opening another catalog through MCP."));
        }

        CatalogLoadResult loaded = await loader.LoadAsync(request.Path, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return WorkspaceError(loaded.Error!);
        }

        WorkspaceResponse response = workspace.Open(
            loaded.CatalogPath!,
            loaded.Catalog!,
            loaded.ContentHash!);
        try
        {
            preferences.RecordLastCatalog(loaded.CatalogPath!);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The open operation remains valid if a personal convenience setting cannot be saved.
        }

        return Results.Ok(response);
    }

    private static IResult SearchEntries(WorkspaceEntrySearchRequest request, CatalogWorkspace workspace) =>
        Execute(() => workspace.SearchEntries(request));

    private static IResult GetEntries(IReadOnlyList<string> ids, CatalogWorkspace workspace) =>
        Execute(() => workspace.GetEntries(ids));

    private static async Task<IResult> PrepareEntryChangesAsync(
        PrepareWorkspaceEntryChangesRequest request,
        CatalogWorkspace workspace,
        UsageIndexReader usages,
        CancellationToken cancellationToken)
    {
        try
        {
            WorkspaceContextResponse context = workspace.GetContext();
            UsageIndexSummaryResponse summary = await usages.ReadSummaryAsync(
                context.CatalogPath,
                cancellationToken);
            bool reliable = summary.Availability == UsageIndexAvailability.Available &&
                string.Equals(summary.Status, "ready", StringComparison.Ordinal) &&
                summary.FailedSourceCount == 0;
            Dictionary<string, int?> usageCounts = request.Changes
                .Where(change => string.Equals(change.Operation, "delete", StringComparison.OrdinalIgnoreCase))
                .Where(change => !string.IsNullOrWhiteSpace(change.Id))
                .Select(change => change.Id!)
                .Distinct(StringComparer.Ordinal)
                .ToDictionary(
                    id => id,
                    id => reliable
                        ? summary.Entries.FirstOrDefault(entry => entry.EntryId == id)?.TotalCount ?? 0
                        : (int?)null,
                    StringComparer.Ordinal);
            bool hasDeletions = request.Changes.Any(change =>
                string.Equals(change.Operation, "delete", StringComparison.OrdinalIgnoreCase));
            WorkspaceChangeSetResponse prepared = workspace.PrepareEntryChanges(
                new PrepareEntryChangesRequest(
                    request.ExpectedRevision,
                    request.Changes,
                    usageCounts,
                    request.AllowDeleteWithUsages,
                    hasDeletions ? CreateUsageRevision(summary) : null));
            return Results.Ok(prepared);
        }
        catch (WorkspaceException exception)
        {
            return WorkspaceError(new WorkspaceErrorResponse(exception.Code, exception.Message));
        }
    }

    private static IResult PrepareLocaleChanges(
        PrepareLocaleChangesRequest request,
        CatalogWorkspace workspace) =>
        Execute(() => workspace.PrepareLocaleChanges(request));

    private static async Task<IResult> ApplyChangeSetAsync(
        ApplyWorkspaceChangeSetRequest request,
        CatalogWorkspace workspace,
        UsageIndexReader usages,
        CancellationToken cancellationToken)
    {
        try
        {
            UsageIndexSummaryResponse summary = await usages.ReadSummaryAsync(
                workspace.GetSnapshot().CatalogPath,
                cancellationToken);
            return Results.Ok(workspace.ApplyChangeSet(
                request.ChangeSetId,
                CreateUsageRevision(summary)));
        }
        catch (WorkspaceException exception)
        {
            return WorkspaceError(new WorkspaceErrorResponse(exception.Code, exception.Message));
        }
    }

    private static async Task<IResult> ReadIssuesAsync(
        WorkspaceIssuesRequest request,
        CatalogWorkspace workspace,
        UsageIndexReader usages,
        CancellationToken cancellationToken)
    {
        try
        {
            CatalogResponse catalog = workspace.GetCatalogSnapshot() ?? throw new WorkspaceException(
                WorkspaceErrorCodes.CatalogNotOpen,
                "No catalog is open in the workspace.");
            UsageIndexSummaryResponse usageSummary = await usages.ReadSummaryAsync(
                workspace.GetSnapshot().CatalogPath,
                cancellationToken);
            var requestedKinds = request.Kinds == null || request.Kinds.Count == 0
                ? null
                : new HashSet<string>(request.Kinds, StringComparer.OrdinalIgnoreCase);
            string usageRevision = CreateUsageRevision(usageSummary);
            int limit = Math.Clamp(request.Limit ?? DefaultIssuePageSize, 1, MaximumIssuePageSize);
            int offset = DecodeIssueCursor(request.Cursor, catalog.Revision, usageRevision);
            var page = new List<WorkspaceIssueResponse>(limit);
            int totalCount = 0;

            if (Includes(requestedKinds, "validation"))
            {
                foreach (CatalogDiagnosticResponse diagnostic in catalog.Diagnostics)
                {
                    if (ShouldIncludeIssue(totalCount++, offset, limit))
                    {
                        page.Add(new WorkspaceIssueResponse(
                            "validation",
                            diagnostic.Code,
                            diagnostic.Severity,
                            diagnostic.Message,
                            diagnostic.Target?.EntryId,
                            diagnostic.Target?.EntryPath,
                            diagnostic.Target?.LocaleId));
                    }
                }
            }

            if (Includes(requestedKinds, "missingLocale"))
            {
                foreach (CatalogEntryResponse entry in catalog.Entries)
                {
                    foreach (CatalogLocaleResponse locale in catalog.Locales)
                    {
                        if (entry.Locales.TryGetValue(locale.Id, out CatalogLocaleValueResponse? value) &&
                            (value.Text != null || value.Asset != null))
                        {
                            continue;
                        }

                        if (ShouldIncludeIssue(totalCount++, offset, limit))
                        {
                            page.Add(new WorkspaceIssueResponse(
                                "missingLocale",
                                "missing_locale_value",
                                "warning",
                                $"Entry '{entry.Path}' has no value for locale '{locale.Id}'.",
                                entry.Id,
                                entry.Path,
                                locale.Id));
                        }
                    }
                }
            }

            if (Includes(requestedKinds, "incomplete"))
            {
                foreach (WorkspaceIssueResponse issue in
                         IncompleteLocalizationIssueCollector.Enumerate(catalog))
                {
                    if (ShouldIncludeIssue(totalCount++, offset, limit))
                    {
                        page.Add(issue);
                    }
                }
            }

            bool usageReliable = usageSummary.Availability == UsageIndexAvailability.Available &&
                string.Equals(usageSummary.Status, "ready", StringComparison.Ordinal) &&
                usageSummary.FailedSourceCount == 0;
            Dictionary<string, int> usageCounts = usageSummary.Entries.ToDictionary(
                entry => entry.EntryId,
                entry => entry.TotalCount,
                StringComparer.Ordinal);
            if (usageReliable && Includes(requestedKinds, "unused"))
            {
                foreach (CatalogEntryResponse entry in catalog.Entries)
                {
                    if (usageCounts.TryGetValue(entry.Id, out int count) && count != 0)
                    {
                        continue;
                    }

                    if (ShouldIncludeIssue(totalCount++, offset, limit))
                    {
                        page.Add(new WorkspaceIssueResponse(
                            "unused",
                            "unused_entry",
                            "warning",
                            $"Entry '{entry.Path}' has no indexed usages.",
                            entry.Id,
                            entry.Path,
                            UsageCount: 0));
                    }
                }
            }

            if (Includes(requestedKinds, "danglingUsage"))
            {
                var catalogIds = new HashSet<string>(catalog.Entries.Select(entry => entry.Id), StringComparer.Ordinal);
                foreach (UsageEntrySummaryResponse entry in usageSummary.Entries)
                {
                    if (catalogIds.Contains(entry.EntryId))
                    {
                        continue;
                    }

                    if (ShouldIncludeIssue(totalCount++, offset, limit))
                    {
                        page.Add(new WorkspaceIssueResponse(
                            "danglingUsage",
                            "usage_entry_missing",
                            "error",
                            $"Entry ID {entry.EntryId} is used in the Unity project but does not exist in the catalog.",
                            entry.EntryId,
                            UsageCount: entry.TotalCount));
                    }
                }
            }

            int nextOffset = offset + page.Count;
            return Results.Ok(new WorkspaceIssuesResponse(
                catalog.Revision,
                usageSummary.Availability,
                usageSummary.Status,
                totalCount,
                page,
                nextOffset < totalCount
                    ? EncodeIssueCursor(catalog.Revision, usageRevision, nextOffset)
                    : null));
        }
        catch (WorkspaceException exception)
        {
            return WorkspaceError(new WorkspaceErrorResponse(exception.Code, exception.Message));
        }
    }

    private static bool Includes(HashSet<string>? kinds, string kind) => kinds == null || kinds.Contains(kind);

    private static bool ShouldIncludeIssue(int index, int offset, int limit) =>
        index >= offset && index - offset < limit;

    private static string CreateUsageRevision(UsageIndexSummaryResponse summary) => string.Create(
        CultureInfo.InvariantCulture,
        $"{summary.Availability}\n{summary.Status}\n{summary.UpdatedAtUtc?.ToUnixTimeMilliseconds() ?? -1}\n{summary.FailedSourceCount}");

    private static string EncodeIssueCursor(long revision, string usageRevision, int offset)
    {
        string value = string.Create(CultureInfo.InvariantCulture, $"{revision}\n{offset}\n{usageRevision}");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static int DecodeIssueCursor(string? cursor, long revision, string usageRevision)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return 0;
        }

        try
        {
            string normalized = cursor.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
            string[] parts = Encoding.UTF8.GetString(Convert.FromBase64String(normalized)).Split('\n');
            if (parts.Length != 6 ||
                !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out long cursorRevision) ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int offset) ||
                offset < 0)
            {
                throw new FormatException();
            }

            if (cursorRevision != revision)
            {
                throw new WorkspaceException(
                    WorkspaceErrorCodes.CatalogRevisionMismatch,
                    "The issue cursor belongs to an older workspace revision. Start again.");
            }

            string cursorUsageRevision = string.Join("\n", parts.Skip(2));
            if (!string.Equals(cursorUsageRevision, usageRevision, StringComparison.Ordinal))
            {
                throw new WorkspaceException(
                    WorkspaceErrorCodes.CatalogRevisionMismatch,
                    "The Unity usage index changed while paging issues. Start again.");
            }

            return offset;
        }
        catch (WorkspaceException)
        {
            throw;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new WorkspaceException(WorkspaceErrorCodes.InvalidCursor, "The issue cursor is invalid.");
        }
    }

    private static IResult Execute<T>(Func<T> operation)
    {
        try
        {
            return Results.Ok(operation());
        }
        catch (WorkspaceException exception)
        {
            return WorkspaceError(new WorkspaceErrorResponse(exception.Code, exception.Message));
        }
    }

    private static IResult WorkspaceError(WorkspaceErrorResponse error)
    {
        int statusCode = error.Code switch
        {
            WorkspaceErrorCodes.CatalogNotFound => StatusCodes.Status404NotFound,
            WorkspaceErrorCodes.CatalogRevisionMismatch or
                WorkspaceErrorCodes.CatalogChangedExternally or
                WorkspaceErrorCodes.WorkspaceHasUnsavedChanges or
                WorkspaceErrorCodes.ChangeSetExpired or
                WorkspaceErrorCodes.UsageIndexChanged => StatusCodes.Status409Conflict,
            WorkspaceErrorCodes.ChangeSetNotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status422UnprocessableEntity,
        };
        return Results.Json(error, statusCode: statusCode);
    }
}
