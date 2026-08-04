using System.CommandLine;
using GreenBox.I18n;

namespace GreenBox.I18n.Cli;

internal static class SearchCommand
{
    public static Command Create()
    {
        var catalogArgument = new Argument<FileInfo>("catalog")
        {
            Description = "Path to the i18n catalog JSON file.",
        };
        var queryArgument = new Argument<string>("query")
        {
            Description = "Text to find in paths, comments, and localized text.",
        };
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Write a machine-readable JSON result.",
        };
        var command = new Command("search", "Search entries in an i18n catalog.")
        {
            Arguments = { catalogArgument, queryArgument },
            Options = { jsonOption },
        };

        command.SetAction(parseResult => Execute(
            parseResult.GetValue(catalogArgument)!,
            parseResult.GetValue(queryArgument)!,
            parseResult.GetValue(jsonOption),
            Console.Out,
            Console.Error));

        return command;
    }

    internal static int Execute(
        FileInfo catalogFile,
        string query,
        bool writeJson,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            var error = new CliError
            {
                Code = CliDiagnosticCodes.EmptyQuery,
                Message = "A search query cannot be empty.",
            };

            if (writeJson)
            {
                standardOutput.WriteLine(CliJson.Serialize(new CliErrorReport
                {
                    File = catalogFile.FullName,
                    Error = error,
                }));
            }
            else
            {
                standardError.WriteLine($"ERROR [{error.Code}] {error.Message}");
            }

            return CliExitCodes.ExecutionError;
        }

        CatalogLoadResult loadResult = CatalogLoader.Load(catalogFile);
        if (loadResult.Catalog == null)
        {
            return QueryCommandOutput.WriteLoadError(
                catalogFile,
                loadResult,
                writeJson,
                standardOutput,
                standardError);
        }

        IReadOnlyList<I18nEntry> entries = loadResult.Catalog.Search(query);
        if (writeJson)
        {
            standardOutput.WriteLine(CliJson.Serialize(new SearchReport
            {
                File = catalogFile.FullName,
                Query = query,
                Count = entries.Count,
                Entries = entries.Select(EntryReport.Create).ToArray(),
            }));
        }
        else
        {
            standardOutput.WriteLine(entries.Count == 1 ? "1 match." : $"{entries.Count} matches.");
            foreach (I18nEntry entry in entries)
            {
                standardOutput.WriteLine($"{entry.Id}  {entry.Path}");
            }
        }

        return CliExitCodes.Success;
    }
}
