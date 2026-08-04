namespace GreenBox.I18n.Cli;

internal sealed class SearchReport
{
    public required string File { get; init; }

    public required string Query { get; init; }

    public required int Count { get; init; }

    public required IReadOnlyList<EntryReport> Entries { get; init; }
}
