using System.ComponentModel;
using GreenBox.I18n.Workspace.Contracts;
using ModelContextProtocol.Server;

namespace GreenBox.I18n.Mcp;

[McpServerToolType]
public sealed class ChangeTools
{
    private readonly GreenBoxHostClient _host;

    public ChangeTools(GreenBoxHostClient host)
    {
        _host = host;
    }

    [McpServerTool(Name = "prepare_entry_changes", ReadOnly = true, Destructive = false, Idempotent = false, UseStructuredContent = true)]
    [Description("Prepares a validated batch of entry creates, updates, and deletes without writing the catalog. Review blockers, warnings, and the diff before apply_change_set.")]
    public Task<McpToolResponse<WorkspaceChangeSetResponse>> PrepareEntryChanges(
        [Description("Current revision from get_workspace.")] long expectedRevision,
        [Description("Batch operations. Use create, update, or delete. Fields change only when their Set... flag is true.")] IReadOnlyList<WorkspaceEntryMutation> changes,
        [Description("Explicitly permit deletion of entries with known usages. Usage-unknown deletion remains blocked.")] bool allowDeleteWithUsages = false,
        CancellationToken cancellationToken = default) =>
        _host.SafeAsync(() => _host.PrepareEntryChangesAsync(
            new McpPrepareEntryChangesRequest(expectedRevision, changes, allowDeleteWithUsages),
            cancellationToken));

    [McpServerTool(Name = "prepare_locale_changes", ReadOnly = true, Destructive = false, Idempotent = false, UseStructuredContent = true)]
    [Description("Prepares a complete locale-definition replacement without writing. Existing locale IDs can be renamed or removed atomically.")]
    public Task<McpToolResponse<WorkspaceChangeSetResponse>> PrepareLocaleChanges(
        [Description("Current revision from get_workspace.")] long expectedRevision,
        [Description("Default locale ID after the change.")] string defaultLocale,
        [Description("Complete locale list after the change.")] IReadOnlyList<CatalogLocaleEditRequest> locales,
        [Description("Locale ID renames to propagate through entry values.")] IReadOnlyList<CatalogLocaleRenameRequest>? renames = null,
        [Description("Locale IDs to remove from definitions and entries.")] IReadOnlyList<string>? removedIds = null,
        CancellationToken cancellationToken = default) =>
        _host.SafeAsync(() => _host.PrepareLocaleChangesAsync(
            new PrepareLocaleChangesRequest(
                expectedRevision,
                defaultLocale,
                locales,
                renames ?? Array.Empty<CatalogLocaleRenameRequest>(),
                removedIds ?? Array.Empty<string>()),
            cancellationToken));

    [McpServerTool(Name = "apply_change_set", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true)]
    [Description("Atomically validates, applies, and saves a previously prepared change set. It fails if revision, source file, or Web unsaved state changed.")]
    public Task<McpToolResponse<ApplyWorkspaceChangeSetResponse>> ApplyChangeSet(
        [Description("changeSetId returned by a prepare tool.")] string changeSetId,
        CancellationToken cancellationToken) =>
        _host.SafeAsync(() => _host.ApplyChangeSetAsync(changeSetId, cancellationToken));
}
