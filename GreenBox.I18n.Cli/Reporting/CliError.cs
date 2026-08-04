namespace GreenBox.I18n.Cli;

internal sealed class CliError
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public int? Line { get; init; }

    public int? Position { get; init; }
}

internal sealed class CliErrorReport
{
    public required string File { get; init; }

    public required CliError Error { get; init; }
}
