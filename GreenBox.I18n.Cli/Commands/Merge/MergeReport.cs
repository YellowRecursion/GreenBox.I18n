namespace GreenBox.I18n.Cli;

internal sealed class MergeReport
{
    public required string BaseFile { get; init; }

    public required string CurrentFile { get; init; }

    public required string IncomingFile { get; init; }

    public required string OutputFile { get; init; }

    public required bool Merged { get; init; }

    public required IReadOnlyList<MergeConflictReport> Conflicts { get; init; }
}

internal sealed class MergeConflictReport
{
    public required string JsonPath { get; init; }

    public required string Message { get; init; }
}
