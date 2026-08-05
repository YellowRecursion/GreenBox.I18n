using System.CommandLine;
using GreenBox.I18n;

namespace GreenBox.I18n.Cli;

internal static class AddCommand
{
    public static Command Create()
    {
        var catalogArgument = new Argument<FileInfo>("catalog")
        {
            Description = "Path to the i18n catalog JSON file.",
        };
        var pathArgument = new Argument<string>("path")
        {
            Description = "Full logical path of the new entry.",
        };
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Write a machine-readable JSON result.",
        };
        var command = new Command("add", "Add an empty entry and allocate its stable numeric ID.")
        {
            Arguments = { catalogArgument, pathArgument },
            Options = { jsonOption },
        };

        command.SetAction(parseResult => Execute(
            parseResult.GetValue(catalogArgument)!,
            parseResult.GetValue(pathArgument)!,
            parseResult.GetValue(jsonOption),
            Console.Out,
            Console.Error));

        return command;
    }

    internal static int Execute(
        FileInfo catalogFile,
        string path,
        bool writeJson,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (!I18nPathRules.IsValid(path))
        {
            return CommandOutput.WriteError(
                catalogFile,
                I18nEditCodes.InvalidPath,
                "The path must contain dot-separated identifier segments using Latin letters, digits, and underscores.",
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
        I18nEditResult editResult = catalog.AddEntry(path);
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
        if (writeJson)
        {
            standardOutput.WriteLine(CliJson.Serialize(new AddReport
            {
                File = catalogFile.FullName,
                Id = entry.Id,
                Path = entry.Path,
            }));
        }
        else
        {
            standardOutput.WriteLine($"Added {entry.Id}: {entry.Path}");
        }

        return CliExitCodes.Success;
    }
}
