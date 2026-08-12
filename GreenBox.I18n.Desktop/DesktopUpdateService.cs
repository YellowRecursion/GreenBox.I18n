using Velopack;
using Velopack.Sources;

namespace GreenBox.I18n.Desktop;

internal sealed class DesktopUpdateService
{
    private UpdateManager? _manager;
    private UpdateInfo? _pendingUpdate;

    internal event Action<string>? LogReceived;
    internal event Action<string>? UpdateReady;

    internal bool HasPendingUpdate => _pendingUpdate is not null;

    internal async Task CheckAndDownloadAsync()
    {
        string updateFeedUrl = ProductVersion.UpdateFeedUrl;
        if (string.IsNullOrWhiteSpace(updateFeedUrl))
        {
            Log("Automatic updates are not configured in this development build.");
            return;
        }

        try
        {
            _manager = updateFeedUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)
                ? new UpdateManager(new GithubSource(updateFeedUrl, accessToken: null, prerelease: true))
                : new UpdateManager(updateFeedUrl);
            if (!_manager.IsInstalled)
            {
                Log("Update checks are available after installing GreenBox I18n.");
                return;
            }

            Log("Checking for updates...");
            UpdateInfo? update = await _manager.CheckForUpdatesAsync();
            if (update is null)
            {
                Log("GreenBox I18n is up to date.");
                return;
            }

            string version = update.TargetFullRelease.Version.ToString();
            Log($"Downloading GreenBox I18n {version}...");
            await _manager.DownloadUpdatesAsync(update);
            _pendingUpdate = update;
            Log($"Update {version} is ready. It will be applied when the application exits.");
            UpdateReady?.Invoke(version);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Log($"Update check failed: {exception.Message}");
        }
    }

    internal bool ApplyAndRestart()
    {
        if (_manager is null || _pendingUpdate is null)
        {
            return false;
        }

        _manager.ApplyUpdatesAndRestart(_pendingUpdate);
        return true;
    }

    internal bool ApplyAndExit()
    {
        if (_manager is null || _pendingUpdate is null)
        {
            return false;
        }

        _manager.ApplyUpdatesAndExit(_pendingUpdate);
        return true;
    }

    private void Log(string message) => LogReceived?.Invoke(message);
}
