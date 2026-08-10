using System.CommandLine;

namespace GreenBox.I18n.Cli;

internal static class GitCommand
{
    public static Command Create()
    {
        var command = new Command("git", "Configure Git integration for GreenBox catalogs.");
        command.Subcommands.Add(GitInstallCommand.Create());
        return command;
    }
}
