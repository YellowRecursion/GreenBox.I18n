using System.CommandLine;
using System.CommandLine.Parsing;

namespace GreenBox.I18n.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        RootCommand rootCommand = CliApplication.CreateRootCommand();
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
}
