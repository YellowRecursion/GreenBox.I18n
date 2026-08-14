using System.ComponentModel;
using GreenBox.I18n.Workspace.Contracts;
using ModelContextProtocol.Server;

namespace GreenBox.I18n.Mcp;

[McpServerToolType]
public sealed class WorkspaceTools
{
    private readonly GreenBoxHostClient _host;

    public WorkspaceTools(GreenBoxHostClient host)
    {
        _host = host;
    }

    [McpServerTool(Name = "get_workspace", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Start here automatically for GreenBox.I18n localization work. Returns the shared active catalog, revision, locales, unsaved state, and source-file state.")]
    public Task<McpToolResponse<WorkspaceContextResponse>> GetWorkspace(CancellationToken cancellationToken) =>
        _host.SafeAsync(() => _host.GetWorkspaceAsync(cancellationToken));

    [McpServerTool(Name = "open_catalog", ReadOnly = false, Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Opens a user-selected localization.json in the shared GreenBox.I18n editor and MCP workspace without editing it. If the user has not selected a catalog, ask them to open the intended project in the editor instead.")]
    public Task<McpToolResponse<WorkspaceResponse>> OpenCatalog(
        [Description("Absolute path to localization.json.")] string path,
        CancellationToken cancellationToken) =>
        _host.SafeAsync(() => _host.OpenCatalogAsync(path, cancellationToken));

    [McpServerTool(Name = "get_working_changes", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns unsaved Web changes and whether the source file changed externally. Check this before preparing writes.")]
    public Task<McpToolResponse<WorkspaceWorkingChangesResponse>> GetWorkingChanges(CancellationToken cancellationToken) =>
        _host.SafeAsync(() => _host.GetWorkingChangesAsync(cancellationToken));
}
