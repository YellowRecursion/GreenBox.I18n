using System.CommandLine;

namespace GreenBox.I18n.Cli;

internal static class CliApplication
{
    public static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("GreenBox localization tools for humans, automation, and LLMs.");
        rootCommand.Subcommands.Add(ValidateCommand.Create());
        rootCommand.Subcommands.Add(GetCommand.Create());
        rootCommand.Subcommands.Add(SearchCommand.Create());
        rootCommand.Subcommands.Add(GenerateIdCommand.Create());
        rootCommand.Subcommands.Add(AddCommand.Create());
        rootCommand.Subcommands.Add(RemoveCommand.Create());
        rootCommand.Subcommands.Add(MoveCommand.Create());
        return rootCommand;
    }
}
