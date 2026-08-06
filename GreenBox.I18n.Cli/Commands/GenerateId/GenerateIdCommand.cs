using System.CommandLine;
using System.Globalization;
using GreenBox.I18n;

namespace GreenBox.I18n.Cli;

internal static class GenerateIdCommand
{
    private const int MaximumCount = 10_000;

    public static Command Create()
    {
        var countOption = new Option<int>("--count")
        {
            Description = $"Number of IDs to generate, from 1 to {MaximumCount}.",
            DefaultValueFactory = _ => 1,
        };
        countOption.Validators.Add(result =>
        {
            int count = result.GetValueOrDefault<int>();
            if (count < 1 || count > MaximumCount)
            {
                result.AddError($"Count must be between 1 and {MaximumCount}.");
            }
        });

        var jsonOption = new Option<bool>("--json")
        {
            Description = "Write a machine-readable JSON result.",
        };
        var command = new Command("generate-id", "Generate self-identifying entry IDs.")
        {
            Options = { countOption, jsonOption },
        };

        command.SetAction(parseResult => Execute(
            parseResult.GetValue(countOption),
            parseResult.GetValue(jsonOption),
            Console.Out,
            Console.Error));

        return command;
    }

    internal static int Execute(
        int count,
        bool writeJson,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (count < 1 || count > MaximumCount)
        {
            standardError.WriteLine($"Count must be between 1 and {MaximumCount}.");
            return CliExitCodes.ExecutionError;
        }

        var ids = new HashSet<long>();
        while (ids.Count < count)
        {
            ids.Add(I18nEntryId.Generate());
        }

        string[] formattedIds = ids
            .Select(id => id.ToString(CultureInfo.InvariantCulture))
            .ToArray();

        if (writeJson)
        {
            standardOutput.WriteLine(CliJson.Serialize(new GenerateIdReport
            {
                Count = formattedIds.Length,
                Ids = formattedIds,
            }));
        }
        else
        {
            foreach (string id in formattedIds)
            {
                standardOutput.WriteLine(id);
            }
        }

        return CliExitCodes.Success;
    }
}
