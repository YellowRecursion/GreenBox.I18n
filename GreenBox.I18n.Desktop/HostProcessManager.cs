using System.Net.Http.Json;
using System.Net.Sockets;
using GreenBox.I18n.Editor.Host;
using Microsoft.AspNetCore.Builder;

namespace GreenBox.I18n.Desktop;

internal sealed class HostProcessManager : IDisposable
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(1),
    };
    private WebApplication? _ownedHost;

    internal event Action<string>? LogReceived;

    internal bool OwnsHost => _ownedHost is not null;

    internal async Task<bool> EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
        if (await IsReadyAsync(cancellationToken))
        {
            Log("Connected to the running Host.");
            return true;
        }

        if (await IsEditorPortOccupiedAsync(cancellationToken))
        {
            var uri = new Uri(DesktopConstants.EditorUrl);
            Log($"Port {uri.Port} is already used by another application. Close it, then click Restart Host.");
            return false;
        }

        try
        {
            _ownedHost = EditorHostApplication.Build(
                ["--urls", DesktopConstants.EditorUrl.TrimEnd('/')],
                Log,
                enableConsoleLogging: false);
            Log("Starting Host...");
            await _ownedHost.StartAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            Log($"Host could not be started: {exception.Message}");
            await DisposeOwnedHostAsync();
            return false;
        }

        for (int attempt = 0; attempt < 100; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsReadyAsync(cancellationToken))
            {
                Log("Host is ready.");
                return true;
            }

            await Task.Delay(100, cancellationToken);
        }

        Log("Host did not become ready within 10 seconds.");
        await DisposeOwnedHostAsync();
        return false;
    }

    internal async Task<bool> RestartAsync(CancellationToken cancellationToken = default)
    {
        if (!OwnsHost)
        {
            return await EnsureStartedAsync(cancellationToken);
        }

        await DisposeOwnedHostAsync();
        return await EnsureStartedAsync(cancellationToken);
    }

    internal void StopOwnedHost()
    {
        DisposeOwnedHostAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        StopOwnedHost();
        _httpClient.Dispose();
    }

    private async Task DisposeOwnedHostAsync()
    {
        WebApplication? host = _ownedHost;
        _ownedHost = null;
        if (host is null)
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await host.StopAsync(timeout.Token);
        }
        finally
        {
            await host.DisposeAsync();
        }

        Log("Host stopped.");
    }

    private async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        try
        {
            HostHealth? health = await _httpClient.GetFromJsonAsync<HostHealth>(
                DesktopConstants.HealthUrl,
                cancellationToken);
            return string.Equals(health?.Product, "GreenBox.I18n", StringComparison.Ordinal) &&
                   string.Equals(health?.Status, "ready", StringComparison.Ordinal);
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static async Task<bool> IsEditorPortOccupiedAsync(CancellationToken cancellationToken)
    {
        var uri = new Uri(DesktopConstants.EditorUrl);
        using var client = new TcpClient();

        try
        {
            await client.ConnectAsync(uri.Host, uri.Port, cancellationToken);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private void Log(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            LogReceived?.Invoke(message);
        }
    }

    private sealed record HostHealth(string Product, string Status, string Version);
}
