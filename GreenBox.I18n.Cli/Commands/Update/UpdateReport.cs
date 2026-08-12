namespace GreenBox.I18n.Cli;

internal sealed class UpdateReport
{
    public required string File { get; init; }

    public required bool Changed { get; init; }

    public required EntryReport Entry { get; init; }
}
