using System.CommandLine;
using GreenBox.I18n;

namespace GreenBox.I18n.Cli;

internal static class GetCommand
{
    public static Command Create()
    {
        var catalogArgument = new Argument<FileInfo>("catalog")
        {
            Description = "Path to the i18n catalog JSON file.",
        };
        var idArgument = new Argument<long>("id")
        {
            Description = "Positive numeric ID of the entry.",
        };
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Write a machine-readable JSON result.",
        };
        var command = new Command("get", "Get an i18n entry by its stable ID.")
        {
            Arguments = { catalogArgument, idArgument },
            Options = { jsonOption },
        };

        command.SetAction(parseResult => Execute(
            parseResult.GetValue(catalogArgument)!,
            parseResult.GetValue(idArgument),
            parseResult.GetValue(jsonOption),
            Console.Out,
            Console.Error));

        return command;
    }

    internal static int Execute(
        FileInfo catalogFile,
        long id,
        bool writeJson,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (!I18nEntryId.IsValid(id))
        {
            return CommandOutput.WriteError(
                catalogFile,
                CliDiagnosticCodes.InvalidId,
                $"Entry ID format is invalid: {id}.",
                CliExitCodes.ExecutionError,
                writeJson,
                standardOutput,
                standardError);
        }

        CatalogLoadResult loadResult = CatalogLoader.Load(catalogFile);
        if (loadResult.Catalog == null)
        {
            return CommandOutput.WriteLoadError(
                catalogFile,
                loadResult,
                writeJson,
                standardOutput,
                standardError);
        }

        I18nEntry? entry = loadResult.Catalog.FindById(id);
        if (entry == null)
        {
            return CommandOutput.WriteError(
                catalogFile,
                CliDiagnosticCodes.EntryNotFound,
                $"Entry with ID {id} was not found.",
                CliExitCodes.InvalidData,
                writeJson,
                standardOutput,
                standardError);
        }

        if (writeJson)
        {
            standardOutput.WriteLine(CliJson.Serialize(new GetReport
            {
                File = catalogFile.FullName,
                Entry = EntryReport.Create(entry),
            }));
        }
        else
        {
            CommandOutput.WriteEntry(entry, standardOutput);
        }

        return CliExitCodes.Success;
    }

}
