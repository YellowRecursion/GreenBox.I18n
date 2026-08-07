using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Editor.Host.Editor;

namespace GreenBox.I18n.Editor.Host.Infrastructure;

/// <summary>
/// Validates an indexed location before asking the associated Unity project to reveal it.
/// </summary>
public sealed class UsageNavigationService
{
    private readonly UsageIndexReader _usageIndex;
    private readonly UnityProjectPresenceService _presence;
    private readonly UnityEditorCommandTransport _transport;

    /// <summary>
    /// Creates a usage-navigation service.
    /// </summary>
    public UsageNavigationService(
        UsageIndexReader usageIndex,
        UnityProjectPresenceService presence,
        UnityEditorCommandTransport transport)
    {
        _usageIndex = usageIndex;
        _presence = presence;
        _transport = transport;
    }

    /// <summary>
    /// Opens a location only when it still belongs to the current usage index.
    /// </summary>
    public async Task<UsageNavigationResult> OpenAsync(
        string? catalogPath,
        long entryId,
        string locationId,
        CancellationToken cancellationToken)
    {
        UnityProjectStatusResponse project = _presence.GetStatus(catalogPath);
        if (!project.IsUnityProject || project.ProjectPath == null)
        {
            return UsageNavigationResult.Failure(
                EditorErrorCodes.UnityProjectNotFound,
                "The open catalog is not located inside a Unity project.");
        }

        if (!project.IsEditorOnline)
        {
            return UsageNavigationResult.Failure(
                EditorErrorCodes.UnityEditorOffline,
                "The associated Unity project is offline.");
        }

        UsageEntryResponse usages = await _usageIndex.ReadEntryAsync(
            catalogPath,
            entryId,
            cancellationToken);
        if (usages.Availability != UsageIndexAvailability.Available)
        {
            return UsageNavigationResult.Failure(
                EditorErrorCodes.UsageLocationNotFound,
                "Usage locations are not currently available.");
        }

        CodeUsageResponse? code = usages.Code.FirstOrDefault(candidate =>
            string.Equals(candidate.LocationId, locationId, StringComparison.Ordinal));
        AssetUsageResponse? asset = usages.Assets.FirstOrDefault(candidate =>
            string.Equals(candidate.LocationId, locationId, StringComparison.Ordinal));
        UnityEditorCommand? command = code != null
            ? UnityEditorCommand.OpenCode(code)
            : asset != null
                ? UnityEditorCommand.OpenAsset(asset)
                : null;
        if (command == null)
        {
            return UsageNavigationResult.Failure(
                EditorErrorCodes.UsageLocationNotFound,
                "The usage location is no longer present in the current index.");
        }

        UnityEditorCommandTransportResult result = await _transport.SendAsync(
            project.ProjectPath,
            command,
            cancellationToken);
        return result.Response != null
            ? UsageNavigationResult.Success(result.Response)
            : new UsageNavigationResult(null, result.Error);
    }
}

/// <summary>
/// Contains either a Unity navigation response or a stable Host error.
/// </summary>
public sealed record UsageNavigationResult(
    OpenUsageResponse? Response,
    EditorErrorResponse? Error)
{
    internal static UsageNavigationResult Success(OpenUsageResponse response) => new(response, null);

    internal static UsageNavigationResult Failure(string code, string message) =>
        new(null, new EditorErrorResponse(code, message));
}
