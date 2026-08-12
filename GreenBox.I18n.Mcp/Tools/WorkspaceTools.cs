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
    [Description("Returns the active GreenBox catalog, revision, locales, unsaved state, and source-file state. Call this before catalog work.")]
    public Task<McpToolResponse<WorkspaceContextResponse>> GetWorkspace(CancellationToken cancellationToken) =>
        _host.SafeAsync(() => _host.GetWorkspaceAsync(cancellationToken));

    [McpServerTool(Name = "open_catalog", ReadOnly = false, Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Opens a localization.json in the shared GreenBox workspace. This changes the active catalog for both Web and MCP but does not edit the file.")]
    public Task<McpToolResponse<WorkspaceResponse>> OpenCatalog(
        [Description("Absolute path to localization.json.")] string path,
        CancellationToken cancellationToken) =>
        _host.SafeAsync(() => _host.OpenCatalogAsync(path, cancellationToken));

    [McpServerTool(Name = "get_working_changes", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns unsaved Web changes and whether the source file changed externally. Check this before preparing writes.")]
    public Task<McpToolResponse<WorkspaceWorkingChangesResponse>> GetWorkingChanges(CancellationToken cancellationToken) =>
        _host.SafeAsync(() => _host.GetWorkingChangesAsync(cancellationToken));
}
