using System.Diagnostics;
using System.Text.Json;
using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Editor.Host.Editor;

namespace GreenBox.I18n.Editor.Host.Infrastructure;

/// <summary>
/// Exchanges infrequent, project-scoped commands with the running Unity Editor through Library.
/// </summary>
public sealed class UnityEditorCommandTransport
{
    private const string CommandFormat = "greenbox.i18n.editor-command";
    private const int CommandFormatVersion = 1;
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Sends a validated navigation command and waits for Unity's bounded response.
    /// </summary>
    internal async Task<UnityEditorCommandTransportResult> SendAsync(
        string projectPath,
        UnityEditorCommand command,
        CancellationToken cancellationToken)
    {
        string bridgePath = Path.Combine(projectPath, "Library", "GreenBox.I18n", "Bridge");
        string requestsPath = Path.Combine(bridgePath, "requests");
        string responsesPath = Path.Combine(bridgePath, "responses");
        Directory.CreateDirectory(requestsPath);
        Directory.CreateDirectory(responsesPath);

        string commandId = Guid.NewGuid().ToString("N");
        string requestPath = Path.Combine(requestsPath, $"{commandId}.json");
        string responsePath = Path.Combine(responsesPath, $"{commandId}.json");
        var envelope = new UnityEditorCommandEnvelope(
            CommandFormat,
            CommandFormatVersion,
            commandId,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            command.Type,
            command.FilePath,
            command.Line,
            command.AssetPath,
            command.AssetGuid,
            command.AssetLocalId,
            command.GameObjectLocalId,
            command.IsPrefabOverride,
            command.TargetAssetGuid,
            command.TargetLocalId,
            command.ObjectPath,
            command.ComponentType);

        try
        {
            await WriteAtomicallyAsync(
                requestPath,
                JsonSerializer.Serialize(envelope, JsonOptions),
                cancellationToken);

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < ResponseTimeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(responsePath))
                {
                    try
                    {
                        string json = await File.ReadAllTextAsync(responsePath, cancellationToken);
                        UnityEditorCommandResponse? response =
                            JsonSerializer.Deserialize<UnityEditorCommandResponse>(json, JsonOptions);
                        if (response?.CommandId == commandId)
                        {
                            return UnityEditorCommandTransportResult.Success(
                                new OpenUsageResponse(
                                    response.Status,
                                    string.IsNullOrWhiteSpace(response.Message) ? null : response.Message));
                        }
                    }
                    catch (IOException)
                    {
                        // Unity may still be atomically publishing the response.
                    }
                    catch (JsonException)
                    {
                        // Retry until the bounded timeout instead of exposing a partial file.
                    }
                }

                await Task.Delay(50, cancellationToken);
            }

            return UnityEditorCommandTransportResult.Failure(
                EditorErrorCodes.UnityEditorCommandTimeout,
                "Unity did not answer the navigation command in time.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return UnityEditorCommandTransportResult.Failure(
                EditorErrorCodes.UnityEditorCommandFailed,
                exception.Message);
        }
        finally
        {
            TryDelete(requestPath);
            TryDelete(responsePath);
        }
    }

    private static async Task WriteAtomicallyAsync(
        string destinationPath,
        string content,
        CancellationToken cancellationToken)
    {
        string temporaryPath = destinationPath + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, content, cancellationToken);
            File.Move(temporaryPath, destinationPath, true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record UnityEditorCommandEnvelope(
        string Format,
        int Version,
        string CommandId,
        long CreatedAtUnixMilliseconds,
        string Type,
        string? FilePath,
        int? Line,
        string? AssetPath,
        string? AssetGuid,
        string? AssetLocalId,
        string? GameObjectLocalId,
        bool IsPrefabOverride,
        string? TargetAssetGuid,
        string? TargetLocalId,
        string? ObjectPath,
        string? ComponentType);

    private sealed record UnityEditorCommandResponse(
        string CommandId,
        string Status,
        string? Message);
}

internal sealed record UnityEditorCommand(
    string Type,
    string? FilePath,
    int? Line,
    string? AssetPath,
    string? AssetGuid,
    string? AssetLocalId,
    string? GameObjectLocalId,
    bool IsPrefabOverride,
    string? TargetAssetGuid,
    string? TargetLocalId,
    string? ObjectPath,
    string? ComponentType)
{
    internal static UnityEditorCommand OpenCode(CodeUsageResponse usage) =>
        new("open-code", usage.FilePath, usage.Line, null, null, null, null, false, null, null, null, null);

    internal static UnityEditorCommand OpenAsset(AssetUsageResponse usage) =>
        new(
            "open-asset",
            null,
            null,
            usage.AssetPath,
            usage.AssetGuid,
            usage.AssetLocalId,
            usage.GameObjectLocalId,
            usage.IsPrefabOverride,
            usage.TargetAssetGuid,
            usage.TargetLocalId,
            usage.ObjectPath,
            usage.ComponentType);
}

internal sealed record UnityEditorCommandTransportResult(
    OpenUsageResponse? Response,
    EditorErrorResponse? Error)
{
    internal static UnityEditorCommandTransportResult Success(OpenUsageResponse response) =>
        new(response, null);

    internal static UnityEditorCommandTransportResult Failure(string code, string message) =>
        new(null, new EditorErrorResponse(code, message));
}
