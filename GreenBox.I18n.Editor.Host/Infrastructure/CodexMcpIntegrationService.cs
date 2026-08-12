using System.Diagnostics;
using System.Text.Json;
using GreenBox.I18n.Editor.Host.Contracts;

namespace GreenBox.I18n.Editor.Host.Infrastructure;

/// <summary>
/// Detects and configures the GreenBox MCP server through the official Codex CLI.
/// </summary>
public sealed class CodexMcpIntegrationService
{
    private const string ServerName = "greenbox-i18n";
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Returns the current integration state without changing Codex configuration.
    /// </summary>
    public async Task<CodexIntegrationStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        IntegrationPaths paths = ResolvePaths();
        if (paths.LauncherPath is null)
        {
            return Unavailable(
                "GreenBox Desktop Tools launcher was not found. Reinstall the application.",
                paths);
        }

        if (paths.CodexCliPath is null)
        {
            return Unavailable(
                "Codex CLI was not found on this computer.",
                paths);
        }

        CommandResult result = await RunAsync(
            paths.CodexCliPath,
            ["mcp", "get", ServerName, "--json"],
            cancellationToken);
        if (result.ExitCode != 0)
        {
            return new CodexIntegrationStatusResponse(
                "notConfigured",
                IsConfigured: false,
                CanConfigure: true,
                RestartRequired: false,
                "Codex MCP is not connected yet.",
                BuildSetupCommand(paths.LauncherPath));
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
            JsonElement root = document.RootElement;
            JsonElement transport = root.GetProperty("transport");
            string? command = transport.GetProperty("command").GetString();
            string[] arguments = transport.TryGetProperty("args", out JsonElement args)
                ? args.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray()
                : [];
            bool enabled = !root.TryGetProperty("enabled", out JsonElement enabledElement) ||
                           enabledElement.GetBoolean();
            bool matches = enabled &&
                           PathsEqual(command, paths.LauncherPath) &&
                           arguments.SequenceEqual(["mcp"], StringComparer.Ordinal);

            return new CodexIntegrationStatusResponse(
                matches ? "configured" : "needsRepair",
                IsConfigured: matches,
                CanConfigure: true,
                RestartRequired: false,
                matches
                    ? "Codex can use GreenBox localization tools."
                    : "The existing Codex MCP configuration points to another command.",
                BuildSetupCommand(paths.LauncherPath));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return new CodexIntegrationStatusResponse(
                "error",
                IsConfigured: false,
                CanConfigure: false,
                RestartRequired: false,
                "Codex returned an unreadable MCP configuration.",
                BuildSetupCommand(paths.LauncherPath));
        }
    }

    /// <summary>
    /// Registers the stable GreenBox launcher as a Codex stdio MCP server.
    /// </summary>
    public async Task<CodexIntegrationStatusResponse> ConfigureAsync(
        CancellationToken cancellationToken = default)
    {
        IntegrationPaths paths = ResolvePaths();
        if (paths.LauncherPath is null || paths.CodexCliPath is null)
        {
            return await GetStatusAsync(cancellationToken);
        }

        CodexIntegrationStatusResponse current = await GetStatusAsync(cancellationToken);
        if (current.IsConfigured)
        {
            return current;
        }

        CommandResult existing = await RunAsync(
            paths.CodexCliPath,
            ["mcp", "get", ServerName, "--json"],
            cancellationToken);
        if (existing.ExitCode == 0)
        {
            CommandResult remove = await RunAsync(
                paths.CodexCliPath,
                ["mcp", "remove", ServerName],
                cancellationToken);
            if (remove.ExitCode != 0)
            {
                return Failed(remove, paths);
            }
        }

        CommandResult add = await RunAsync(
            paths.CodexCliPath,
            ["mcp", "add", ServerName, "--", paths.LauncherPath, "mcp"],
            cancellationToken);
        if (add.ExitCode != 0)
        {
            return Failed(add, paths);
        }

        return new CodexIntegrationStatusResponse(
            "configured",
            IsConfigured: true,
            CanConfigure: true,
            RestartRequired: true,
            "Connected. Restart Codex once to load GreenBox tools.",
            BuildSetupCommand(paths.LauncherPath));
    }

    private static IntegrationPaths ResolvePaths()
    {
        string localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        string launcherPath = Path.Combine(
            localApplicationData,
            "GreenBox.I18n",
            "GreenBox.I18n.exe");

        string? codexCliPath = ResolveCodexCli(localApplicationData);
        return new IntegrationPaths(
            File.Exists(launcherPath) ? launcherPath : null,
            codexCliPath);
    }

    private static string? ResolveCodexCli(string localApplicationData)
    {
        string? configuredPath = Environment.GetEnvironmentVariable("CODEX_CLI_PATH");
        if (File.Exists(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        string codexBin = Path.Combine(localApplicationData, "OpenAI", "Codex", "bin");
        if (!Directory.Exists(codexBin))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(codexBin, "codex.exe", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => file.FullName)
            .FirstOrDefault();
    }

    private static async Task<CommandResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(CommandTimeout);
            await process.WaitForExitAsync(timeout.Token);
            return new CommandResult(
                process.ExitCode,
                await standardOutput,
                await standardError);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryTerminate(process);
            return new CommandResult(-1, string.Empty, "Codex CLI did not respond within 15 seconds.");
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new CommandResult(-1, string.Empty, exception.Message);
        }
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static bool PathsEqual(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
    }

    private static string BuildSetupCommand(string? launcherPath)
    {
        string command = launcherPath ?? "<GreenBox.I18n.exe>";
        return $"codex mcp add {ServerName} -- \"{command}\" mcp";
    }

    private static CodexIntegrationStatusResponse Unavailable(
        string message,
        IntegrationPaths paths) =>
        new(
            "unavailable",
            IsConfigured: false,
            CanConfigure: false,
            RestartRequired: false,
            message,
            BuildSetupCommand(paths.LauncherPath));

    private static CodexIntegrationStatusResponse Failed(
        CommandResult result,
        IntegrationPaths paths)
    {
        string message = string.IsNullOrWhiteSpace(result.StandardError)
            ? "Codex MCP could not be configured."
            : result.StandardError.Trim();
        return new CodexIntegrationStatusResponse(
            "error",
            IsConfigured: false,
            CanConfigure: true,
            RestartRequired: false,
            message,
            BuildSetupCommand(paths.LauncherPath));
    }

    private sealed record IntegrationPaths(string? LauncherPath, string? CodexCliPath);

    private sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);
}
