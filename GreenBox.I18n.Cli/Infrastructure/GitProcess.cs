using System.ComponentModel;
using System.Diagnostics;

namespace GreenBox.I18n.Cli;

internal static class GitProcess
{
    public static GitProcessResult Run(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using Process process = Process.Start(startInfo)!;
            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return new GitProcessResult(process.ExitCode, standardOutput.Trim(), standardError.Trim());
        }
        catch (Exception exception) when (exception is Win32Exception or IOException)
        {
            return new GitProcessResult(-1, string.Empty, exception.Message);
        }
    }
}

internal sealed record GitProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool IsSuccess => ExitCode == 0;
}
