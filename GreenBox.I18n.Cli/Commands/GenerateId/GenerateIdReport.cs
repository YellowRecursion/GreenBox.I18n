namespace GreenBox.I18n.Cli;

internal sealed class GenerateIdReport
{
    public required int Count { get; init; }

    public required IReadOnlyList<string> Ids { get; init; }
}
