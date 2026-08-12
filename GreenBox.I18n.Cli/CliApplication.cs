using System.CommandLine;
using System.CommandLine.Parsing;

namespace GreenBox.I18n.Cli;

public static class CliApplication
{
    public static int Run(string[] args)
    {
        RootCommand rootCommand = CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(args);

        if (parseResult.Errors.Count > 0)
        {
            foreach (ParseError error in parseResult.Errors)
            {
                Console.Error.WriteLine(error.Message);
            }

            return CliExitCodes.ExecutionError;
        }

        return parseResult.Invoke();
    }

    public static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("GreenBox localization catalog tools.");
        rootCommand.Subcommands.Add(ValidateCommand.Create());
        rootCommand.Subcommands.Add(GetCommand.Create());
        rootCommand.Subcommands.Add(SearchCommand.Create());
        rootCommand.Subcommands.Add(GenerateIdCommand.Create());
        rootCommand.Subcommands.Add(AddCommand.Create());
        rootCommand.Subcommands.Add(UpdateCommand.Create());
        rootCommand.Subcommands.Add(RemoveCommand.Create());
        rootCommand.Subcommands.Add(MoveCommand.Create());
        rootCommand.Subcommands.Add(MergeCommand.Create());
        rootCommand.Subcommands.Add(GitCommand.Create());
        return rootCommand;
    }
}
