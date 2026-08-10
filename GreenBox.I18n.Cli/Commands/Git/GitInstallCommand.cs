using System.CommandLine;

namespace GreenBox.I18n.Cli;

internal static class GitInstallCommand
{
    private const string DriverName = "greenbox-i18n";
    private const string DefaultDriverExecutable = "i18n";

    public static Command Create()
    {
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Write a machine-readable JSON report.",
        };
        var command = new Command("install", "Install the GreenBox merge driver for the current user.")
        {
            Options = { jsonOption },
        };
        command.SetAction(parseResult => Execute(
            DefaultDriverExecutable,
            GitConfigurationTarget.Global,
            parseResult.GetValue(jsonOption),
            Console.Out,
            Console.Error));
        return command;
    }

    internal static int Execute(
        string driverExecutable,
        GitConfigurationTarget configurationTarget,
        bool writeJson,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        GitProcessResult gitVersion = GitProcess.Run("--version");
        if (!gitVersion.IsSuccess)
        {
            return WriteError(
                configurationTarget,
                CliDiagnosticCodes.GitNotFound,
                string.IsNullOrWhiteSpace(gitVersion.StandardError)
                    ? "Git executable was not found."
                    : gitVersion.StandardError,
                writeJson,
                standardOutput,
                standardError);
        }

        string driverCommand =
            $"{QuoteDriverExecutable(driverExecutable)} merge " +
            "--base \"%O\" --current \"%A\" --incoming \"%B\" --output \"%A\"";
        GitProcessResult nameResult = RunConfig(
            configurationTarget,
            $"merge.{DriverName}.name",
            "GreenBox I18n catalog merge");
        GitProcessResult driverResult = nameResult.IsSuccess
            ? RunConfig(configurationTarget, $"merge.{DriverName}.driver", driverCommand)
            : nameResult;
        if (!driverResult.IsSuccess)
        {
            return WriteError(
                configurationTarget,
                CliDiagnosticCodes.GitConfigurationFailed,
                driverResult.StandardError,
                writeJson,
                standardOutput,
                standardError);
        }

        var report = new GitInstallReport
        {
            Scope = configurationTarget.Name,
            Driver = driverCommand,
            Configured = true,
        };
        if (writeJson)
        {
            standardOutput.WriteLine(CliJson.Serialize(report));
        }
        else
        {
            standardOutput.WriteLine("GreenBox Git merge driver configured for this user.");
        }

        return CliExitCodes.Success;
    }

    private static int WriteError(
        GitConfigurationTarget target,
        string code,
        string message,
        bool writeJson,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        var error = new CliError { Code = code, Message = message };
        if (writeJson)
        {
            standardOutput.WriteLine(CliJson.Serialize(new GitInstallReport
            {
                Scope = target.Name,
                Configured = false,
                Error = error,
            }));
        }
        else
        {
            standardError.WriteLine($"ERROR [{code}] {message}");
        }

        return CliExitCodes.ExecutionError;
    }

    private static GitProcessResult RunConfig(
        GitConfigurationTarget target,
        string key,
        string value)
    {
        return GitProcess.Run(target.Arguments.Concat(new[] { key, value }).ToArray());
    }

    private static string QuoteDriverExecutable(string executable)
    {
        string normalized = Path.IsPathRooted(executable)
            ? executable.Replace('\\', '/')
            : executable;
        return !Path.IsPathRooted(executable) &&
               normalized.IndexOfAny(new[] { ' ', '\t', '\'', '"' }) < 0
            ? normalized
            : "'" + normalized.Replace("'", "'\"'\"'") + "'";
    }
}

internal sealed class GitConfigurationTarget
{
    private GitConfigurationTarget(string name, IReadOnlyList<string> arguments)
    {
        Name = name;
        Arguments = arguments;
    }

    public string Name { get; }

    public IReadOnlyList<string> Arguments { get; }

    public static GitConfigurationTarget Global { get; } =
        new GitConfigurationTarget("global", new[] { "config", "--global" });

    public static GitConfigurationTarget Repository(string repositoryPath)
    {
        return new GitConfigurationTarget(
            "repository",
            new[] { "-C", repositoryPath, "config", "--local" });
    }
}
