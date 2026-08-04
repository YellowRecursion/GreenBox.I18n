using System.CommandLine;

namespace GreenBox.I18n.Cli;

internal static class CliApplication
{
    public static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("GreenBox localization tools for humans, automation, and LLMs.");
        rootCommand.Subcommands.Add(ValidateCommand.Create());
        return rootCommand;
    }
}
