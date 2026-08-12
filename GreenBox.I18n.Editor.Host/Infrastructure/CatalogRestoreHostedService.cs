using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Workspace;

namespace GreenBox.I18n.Editor.Host.Infrastructure;

/// <summary>
/// Restores the last catalog during Host startup without blocking the Desktop UI thread.
/// </summary>
public sealed class CatalogRestoreHostedService(
    EditorPreferencesStore preferences,
    CatalogFileLoader loader,
    CatalogWorkspace workspace) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        EditorPreferencesResponse snapshot = preferences.GetSnapshot();
        if (!snapshot.ReopenLastCatalog || string.IsNullOrWhiteSpace(snapshot.LastCatalogPath))
        {
            return;
        }

        CatalogLoadResult result = await loader
            .LoadAsync(snapshot.LastCatalogPath, cancellationToken)
            .ConfigureAwait(false);
        if (result.IsSuccess)
        {
            workspace.Open(
                result.CatalogPath!,
                result.Catalog!,
                result.ContentHash!);
            preferences.SetRestoreError(null);
        }
        else
        {
            preferences.SetRestoreError(result.Error!.Message);
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
