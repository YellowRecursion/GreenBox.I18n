using System.Net.Http.Json;
using System.Text.Json;
using GreenBox.I18n.Workspace.Contracts;

namespace GreenBox.I18n.Mcp;

public sealed class GreenBoxHostClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public GreenBoxHostClient(HttpClient http)
    {
        _http = http;
    }

    public Task<WorkspaceContextResponse> GetWorkspaceAsync(CancellationToken cancellationToken) =>
        GetAsync<WorkspaceContextResponse>("/api/workspace", cancellationToken);

    public Task<WorkspaceResponse> OpenCatalogAsync(string path, CancellationToken cancellationToken) =>
        PostAsync<WorkspaceResponse>("/api/workspace/open", new { path }, cancellationToken);

    public Task<WorkspaceEntrySearchResponse> SearchEntriesAsync(
        WorkspaceEntrySearchRequest request,
        CancellationToken cancellationToken) =>
        PostAsync<WorkspaceEntrySearchResponse>("/api/workspace/entries/search", request, cancellationToken);

    public Task<WorkspaceEntriesResponse> GetEntriesAsync(
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken) =>
        PostAsync<WorkspaceEntriesResponse>("/api/workspace/entries/get", ids, cancellationToken);

    public Task<WorkspaceWorkingChangesResponse> GetWorkingChangesAsync(CancellationToken cancellationToken) =>
        GetAsync<WorkspaceWorkingChangesResponse>("/api/workspace/changes", cancellationToken);

    public Task<WorkspaceChangeSetResponse> PrepareEntryChangesAsync(
        McpPrepareEntryChangesRequest request,
        CancellationToken cancellationToken) =>
        PostAsync<WorkspaceChangeSetResponse>("/api/workspace/changes/prepare-entries", request, cancellationToken);

    public Task<WorkspaceChangeSetResponse> PrepareLocaleChangesAsync(
        PrepareLocaleChangesRequest request,
        CancellationToken cancellationToken) =>
        PostAsync<WorkspaceChangeSetResponse>("/api/workspace/changes/prepare-locales", request, cancellationToken);

    public Task<ApplyWorkspaceChangeSetResponse> ApplyChangeSetAsync(
        string changeSetId,
        CancellationToken cancellationToken) =>
        PostAsync<ApplyWorkspaceChangeSetResponse>(
            "/api/workspace/changes/apply",
            new ApplyWorkspaceChangeSetRequest(changeSetId),
            cancellationToken);

    public Task<McpWorkspaceIssuesResponse> GetIssuesAsync(
        McpWorkspaceIssuesRequest request,
        CancellationToken cancellationToken) =>
        PostAsync<McpWorkspaceIssuesResponse>("/api/workspace/issues", request, cancellationToken);

    public Task<McpUsageEntryResponse> GetEntryUsagesAsync(
        string entryId,
        CancellationToken cancellationToken) =>
        GetAsync<McpUsageEntryResponse>($"/api/usage-index/entries/{Uri.EscapeDataString(entryId)}", cancellationToken);

    public Task<McpOpenUsageResponse> OpenLocationAsync(
        string entryId,
        string locationId,
        CancellationToken cancellationToken) =>
        PostAsync<McpOpenUsageResponse>(
            "/api/usage-index/open",
            new { entryId, locationId },
            cancellationToken);

    public Task<McpMessageAnalysisResponse> AnalyzeMessageAsync(
        string source,
        CancellationToken cancellationToken) =>
        PostAsync<McpMessageAnalysisResponse>("/api/messages/analyze", new { source }, cancellationToken);

    public Task<McpMessagePreviewResponse> PreviewMessageAsync(
        string source,
        string culture,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken cancellationToken) =>
        PostAsync<McpMessagePreviewResponse>(
            "/api/messages/preview",
            new { source, culture, arguments },
            cancellationToken);

    public async Task<McpToolResponse<T>> SafeAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return McpToolResponse<T>.Ok(await operation());
        }
        catch (HostApiException exception)
        {
            return McpToolResponse<T>.Failed(exception.Error);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return McpToolResponse<T>.Failed(new McpToolError(
                "host_unavailable",
                "GreenBox.I18n is not reachable.",
                true,
                "Start GreenBox.I18n, then retry the operation. " +
                "For development, ensure the Editor Host is listening on GREENBOX_I18N_HOST_URL."));
        }
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync(path, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(path, body, JsonOptions, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            T? result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return result ?? throw new HostApiException(
                "invalid_host_response",
                "The GreenBox Host returned an empty response.",
                true,
                "Retry the operation. If it repeats, restart GreenBox Desktop Tools.");
        }

        McpHostError? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<McpHostError>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            // A stable fallback below is more useful than leaking transport details to the model.
        }

        string code = error?.Code ?? "host_request_failed";
        string message = error?.Message ?? $"GreenBox Host rejected the request with HTTP {(int)response.StatusCode}.";
        throw new HostApiException(code, message, IsRetryable(code), Remediation(code));
    }

    private static bool IsRetryable(string code) => code is
        "catalog_revision_mismatch" or
        "catalog_changed_externally" or
        "change_set_expired" or
        "change_set_not_found" or
        "usage_index_changed" or
        "unity_editor_offline" or
        "unity_editor_command_timeout";

    private static string Remediation(string code) => code switch
    {
        "catalog_not_open" =>
            "Ask the user to open the intended project in the GreenBox.I18n editor, then retry get_workspace. " +
            "Call open_catalog only when the user explicitly selected or provided the catalog path.",
        "catalog_revision_mismatch" => "Read the workspace again and prepare a new change set from its current revision.",
        "workspace_has_unsaved_changes" => "Ask the user to save or revert the Web editor changes before applying MCP writes.",
        "catalog_changed_externally" => "Ask the user to reopen or merge the externally changed catalog, then prepare again.",
        "change_set_expired" or "change_set_not_found" => "Prepare the changes again and apply the new change-set ID.",
        "usage_index_changed" => "The Unity usage index changed. Prepare the deletion again before applying it.",
        "change_set_blocked" => "Inspect the blockers returned by prepare_entry_changes or prepare_locale_changes.",
        "unity_editor_offline" => "Open the matching Unity project before requesting navigation.",
        _ => "Correct the request using the error message, then retry only if appropriate.",
    };
}
