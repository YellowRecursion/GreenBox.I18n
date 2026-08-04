namespace GreenBox.I18n.Cli;

internal sealed class MoveReport
{
    public required string File { get; init; }

    public required string Id { get; init; }

    public required string PreviousPath { get; init; }

    public required string Path { get; init; }

    public required bool Changed { get; init; }
}
