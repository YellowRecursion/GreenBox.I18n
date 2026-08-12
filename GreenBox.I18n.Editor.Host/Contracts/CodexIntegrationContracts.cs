namespace GreenBox.I18n.Editor.Host.Contracts;

/// <summary>
/// Describes the local Codex MCP integration.
/// </summary>
public sealed record CodexIntegrationStatusResponse(
    string Status,
    bool IsConfigured,
    bool CanConfigure,
    bool RestartRequired,
    string Message,
    string? SetupCommand);

/// <summary>
/// Confirms an explicit request to modify the current user's Codex MCP configuration.
/// </summary>
public sealed record ConfigureCodexIntegrationRequest(bool Confirm);
