using System.CommandLine;
using System.Globalization;
using GreenBox.I18n;

namespace GreenBox.I18n.Cli;

internal static class MoveCommand
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
        var pathArgument = new Argument<string>("path")
        {
            Description = "New full logical path of the entry.",
        };
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Write a machine-readable JSON result.",
        };
        var command = new Command("move", "Move or rename an entry by changing its logical path.")
        {
            Arguments = { catalogArgument, idArgument, pathArgument },
            Options = { jsonOption },
        };

        command.SetAction(parseResult => Execute(
            parseResult.GetValue(catalogArgument)!,
            parseResult.GetValue(idArgument),
            parseResult.GetValue(pathArgument)!,
            parseResult.GetValue(jsonOption),
            Console.Out,
            Console.Error));

        return command;
    }

    internal static int Execute(
        FileInfo catalogFile,
        long id,
        string newPath,
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

        if (!I18nPathRules.IsValid(newPath))
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
        string? previousPath = catalog.FindById(id)?.Path;
        I18nEditResult editResult = catalog.MoveEntry(id, newPath);

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

        if (editResult.HasChanges)
        {
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
        }

        string idText = id.ToString(CultureInfo.InvariantCulture);
        if (writeJson)
        {
            standardOutput.WriteLine(CliJson.Serialize(new MoveReport
            {
                File = catalogFile.FullName,
                Id = idText,
                PreviousPath = previousPath!,
                Path = editResult.Entry!.Path,
                Changed = editResult.HasChanges,
            }));
        }
        else if (editResult.HasChanges)
        {
            standardOutput.WriteLine($"Moved {idText}: {previousPath} -> {editResult.Entry!.Path}");
        }
        else
        {
            standardOutput.WriteLine($"Unchanged {idText}: {editResult.Entry!.Path}");
        }

        return CliExitCodes.Success;
    }

}
