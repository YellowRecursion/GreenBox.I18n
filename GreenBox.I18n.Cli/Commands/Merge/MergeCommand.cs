using System.CommandLine;
using GreenBox.I18n;

namespace GreenBox.I18n.Cli;

internal static class MergeCommand
{
    public static Command Create()
    {
        var baseOption = new Option<FileInfo>("--base")
        {
            Description = "Common ancestor catalog.",
            Required = true,
        };
        var currentOption = new Option<FileInfo>("--current")
        {
            Description = "Current local catalog.",
            Required = true,
        };
        var incomingOption = new Option<FileInfo>("--incoming")
        {
            Description = "Incoming catalog.",
            Required = true,
        };
        var outputOption = new Option<FileInfo>("--output")
        {
            Description = "Destination for a successful merge. May be the current catalog.",
            Required = true,
        };
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Write a machine-readable JSON report.",
        };

        var command = new Command("merge", "Safely merge three catalog versions by stable IDs.")
        {
            Options = { baseOption, currentOption, incomingOption, outputOption, jsonOption },
        };
        command.SetAction(parseResult => Execute(
            parseResult.GetValue(baseOption)!,
            parseResult.GetValue(currentOption)!,
            parseResult.GetValue(incomingOption)!,
            parseResult.GetValue(outputOption)!,
            parseResult.GetValue(jsonOption),
            Console.Out,
            Console.Error));
        return command;
    }

    internal static int Execute(
        FileInfo baseFile,
        FileInfo currentFile,
        FileInfo incomingFile,
        FileInfo outputFile,
        bool writeJson,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        CatalogLoadResult baseline = CatalogLoader.Load(baseFile);
        if (baseline.Error != null)
        {
            return CommandOutput.WriteLoadError(
                baseFile, baseline, writeJson, standardOutput, standardError);
        }

        CatalogLoadResult current = CatalogLoader.Load(currentFile);
        if (current.Error != null)
        {
            return CommandOutput.WriteLoadError(
                currentFile, current, writeJson, standardOutput, standardError);
        }

        CatalogLoadResult incoming = CatalogLoader.Load(incomingFile);
        if (incoming.Error != null)
        {
            return CommandOutput.WriteLoadError(
                incomingFile, incoming, writeJson, standardOutput, standardError);
        }

        I18nCatalogMergeResult result = I18nCatalogMerge.Merge(
            baseline.Catalog!,
            current.Catalog!,
            incoming.Catalog!);
        var report = new MergeReport
        {
            BaseFile = baseFile.FullName,
            CurrentFile = currentFile.FullName,
            IncomingFile = incomingFile.FullName,
            OutputFile = outputFile.FullName,
            Merged = result.IsSuccess,
            Conflicts = result.Conflicts.Select(conflict => new MergeConflictReport
            {
                JsonPath = conflict.JsonPath,
                Message = conflict.Message,
            }).ToArray(),
        };

        if (!result.IsSuccess)
        {
            WriteReport(report, writeJson, standardOutput, standardError);
            return CliExitCodes.InvalidData;
        }

        CliError? writeError = CatalogFileWriter.WriteAtomically(
            outputFile,
            I18nCatalogJson.Serialize(result.Catalog!));
        if (writeError != null)
        {
            return CommandOutput.WriteError(
                outputFile,
                writeError.Code,
                writeError.Message,
                CliExitCodes.ExecutionError,
                writeJson,
                standardOutput,
                standardError);
        }

        WriteReport(report, writeJson, standardOutput, standardError);
        return CliExitCodes.Success;
    }

    private static void WriteReport(
        MergeReport report,
        bool writeJson,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (writeJson)
        {
            standardOutput.WriteLine(CliJson.Serialize(report));
            return;
        }

        if (report.Merged)
        {
            standardOutput.WriteLine($"Merged catalog written to '{report.OutputFile}'.");
            return;
        }

        standardError.WriteLine(
            $"Catalog merge has {report.Conflicts.Count} " +
            (report.Conflicts.Count == 1 ? "conflict:" : "conflicts:"));
        foreach (MergeConflictReport conflict in report.Conflicts)
        {
            standardError.WriteLine($"  {conflict.JsonPath}: {conflict.Message}");
        }
    }
}
