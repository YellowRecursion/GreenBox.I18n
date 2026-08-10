#nullable enable

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.VersionControl.Git
{
    internal static class I18nGitIntegration
    {
        internal static I18nGitIntegrationStatus GetStatus()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            ProcessResult git = Run(projectRoot, "git", "--version");
            if (!git.IsSuccess)
            {
                return new I18nGitIntegrationStatus(
                    I18nGitIntegrationState.GitNotFound,
                    "Git is not available on this computer.");
            }

            ProcessResult cli = Run(projectRoot, "i18n", "--help");
            if (!cli.Started)
            {
                return new I18nGitIntegrationStatus(
                    I18nGitIntegrationState.CliNotFound,
                    "GreenBox Desktop Tools or CLI is not available in PATH.");
            }

            ProcessResult driver = Run(
                projectRoot,
                "git",
                "config --global --get merge.greenbox-i18n.driver");
            if (!driver.IsSuccess ||
                driver.StandardOutput.IndexOf(" merge --base ", StringComparison.Ordinal) < 0 ||
                driver.StandardOutput.IndexOf(" --output ", StringComparison.Ordinal) < 0)
            {
                return new I18nGitIntegrationStatus(
                    I18nGitIntegrationState.DriverNotConfigured,
                    "The GreenBox merge driver is not configured for this user.");
            }

            return new I18nGitIntegrationStatus(
                I18nGitIntegrationState.Configured,
                "Git will structurally merge localization.json through GreenBox I18n.");
        }

        internal static bool TryConfigure(out string error)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            ProcessResult result = Run(projectRoot, "i18n", "git install");
            error = result.IsSuccess
                ? string.Empty
                : string.IsNullOrWhiteSpace(result.StandardError)
                    ? "GreenBox could not configure the Git merge driver."
                    : result.StandardError;
            return result.IsSuccess;
        }

        private static ProcessResult Run(string workingDirectory, string executable, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            try
            {
                using Process process = Process.Start(startInfo)!;
                if (!process.WaitForExit(2000))
                {
                    process.Kill();
                    return new ProcessResult(true, -1, string.Empty, "The command timed out.");
                }

                return new ProcessResult(
                    true,
                    process.ExitCode,
                    process.StandardOutput.ReadToEnd(),
                    process.StandardError.ReadToEnd());
            }
            catch (Exception exception) when (exception is Win32Exception or IOException)
            {
                return new ProcessResult(false, -1, string.Empty, exception.Message);
            }
        }

        private readonly struct ProcessResult
        {
            internal ProcessResult(
                bool started,
                int exitCode,
                string standardOutput,
                string standardError)
            {
                Started = started;
                ExitCode = exitCode;
                StandardOutput = standardOutput;
                StandardError = standardError;
            }

            internal bool Started { get; }

            internal int ExitCode { get; }

            internal string StandardOutput { get; }

            internal string StandardError { get; }

            internal bool IsSuccess => Started && ExitCode == 0;
        }
    }

    internal readonly struct I18nGitIntegrationStatus
    {
        internal I18nGitIntegrationStatus(I18nGitIntegrationState state, string message)
        {
            State = state;
            Message = message;
        }

        internal I18nGitIntegrationState State { get; }

        internal string Message { get; }
    }

    internal enum I18nGitIntegrationState
    {
        GitNotFound,
        CliNotFound,
        DriverNotConfigured,
        Configured,
    }
}
