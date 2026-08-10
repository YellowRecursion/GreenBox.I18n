namespace GreenBox.I18n.Cli;

internal sealed class GitInstallReport
{
    public required string Scope { get; init; }

    public string? Driver { get; init; }

    public required bool Configured { get; init; }

    public CliError? Error { get; init; }
}
