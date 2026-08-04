namespace GreenBox.I18n.Cli;

internal sealed class GetReport
{
    public required string File { get; init; }

    public required EntryReport Entry { get; init; }
}
