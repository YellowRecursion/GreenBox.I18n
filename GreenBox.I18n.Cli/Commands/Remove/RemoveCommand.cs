using System.CommandLine;
using System.Globalization;
using GreenBox.I18n;

namespace GreenBox.I18n.Cli;

internal static class RemoveCommand
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
        var command = new Command("remove", "Remove an entry without making its stable ID reusable.")
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
        if (id <= 0)
        {
            return CommandOutput.WriteError(
                catalogFile,
                I18nEditCodes.InvalidId,
                $"Entry ID must be positive: {id}.",
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

        I18nCatalog catalog = loadResult.Catalog;
        I18nEditResult editResult = catalog.RemoveEntry(id);
        if (!editResult.IsSuccess)
        {
            I18nEditError error = editResult.Error!;
            return CommandOutput.WriteError(
                catalogFile,
                error.Code,
                error.Message,
                CliExitCodes.InvalidData,
                writeJson,
                standardOutput,
                standardError);
        }

        CliError? writeError = CatalogFileWriter.WriteAtomically(
            catalogFile,
            I18nCatalogJson.Serialize(catalog));
        if (writeError != null)
        {
            return CommandOutput.WriteError(
                catalogFile,
                writeError.Code,
                writeError.Message,
                CliExitCodes.ExecutionError,
                writeJson,
                standardOutput,
                standardError);
        }

        I18nEntry entry = editResult.Entry!;
        string idText = id.ToString(CultureInfo.InvariantCulture);
        if (writeJson)
        {
            standardOutput.WriteLine(CliJson.Serialize(new RemoveReport
            {
                File = catalogFile.FullName,
                Id = idText,
                Path = entry.Path,
            }));
        }
        else
        {
            standardOutput.WriteLine($"Removed {idText}: {entry.Path}");
        }

        return CliExitCodes.Success;
    }
}
