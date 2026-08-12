using System.Runtime.InteropServices;
using Velopack.Locators;

namespace GreenBox.I18n.Desktop;

internal static class CommandLineIntegration
{
    private const int HwndBroadcast = 0xffff;
    private const uint WmSettingChange = 0x001a;
    private const uint SmtoAbortIfHung = 0x0002;

    internal static void Install()
    {
        IVelopackLocator locator = VelopackLocator.Current;
        string? rootAppDirectory = locator.RootAppDir;
        if (locator.IsPortable || string.IsNullOrWhiteSpace(rootAppDirectory))
        {
            return;
        }

        string binDirectory = Path.Combine(rootAppDirectory, "bin");
        Directory.CreateDirectory(binDirectory);
        File.WriteAllText(
            Path.Combine(binDirectory, "i18n.cmd"),
            "@echo off\r\n\"%~dp0..\\current\\i18n.exe\" %*\r\n");

        string currentPath = Environment.GetEnvironmentVariable(
            "Path",
            EnvironmentVariableTarget.User) ?? string.Empty;
        string updatedPath = AddPathEntry(currentPath, binDirectory);
        if (!string.Equals(currentPath, updatedPath, StringComparison.Ordinal))
        {
            Environment.SetEnvironmentVariable(
                "Path",
                updatedPath,
                EnvironmentVariableTarget.User);
            BroadcastEnvironmentChange();
        }
    }

    internal static void Uninstall()
    {
        IVelopackLocator locator = VelopackLocator.Current;
        string? rootAppDirectory = locator.RootAppDir;
        if (locator.IsPortable || string.IsNullOrWhiteSpace(rootAppDirectory))
        {
            return;
        }

        string binDirectory = Path.Combine(rootAppDirectory, "bin");
        string currentPath = Environment.GetEnvironmentVariable(
            "Path",
            EnvironmentVariableTarget.User) ?? string.Empty;
        string updatedPath = RemovePathEntry(currentPath, binDirectory);
        if (!string.Equals(currentPath, updatedPath, StringComparison.Ordinal))
        {
            Environment.SetEnvironmentVariable(
                "Path",
                updatedPath,
                EnvironmentVariableTarget.User);
            BroadcastEnvironmentChange();
        }
    }

    internal static string AddPathEntry(string path, string entry)
    {
        if (SplitPath(path).Any(existing => PathsEqual(existing, entry)))
        {
            return path;
        }

        return string.IsNullOrWhiteSpace(path)
            ? entry
            : path.TrimEnd(';') + ";" + entry;
    }

    internal static string RemovePathEntry(string path, string entry) =>
        string.Join(
            ";",
            SplitPath(path).Where(existing => !PathsEqual(existing, entry)));

    private static IEnumerable<string> SplitPath(string path) =>
        path.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            NormalizePath(left),
            NormalizePath(right),
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizePath(string path) =>
        Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static void BroadcastEnvironmentChange()
    {
        SendMessageTimeout(
            (nint)HwndBroadcast,
            WmSettingChange,
            nint.Zero,
            "Environment",
            SmtoAbortIfHung,
            1000,
            out _);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint SendMessageTimeout(
        nint window,
        uint message,
        nint wParam,
        string lParam,
        uint flags,
        uint timeout,
        out nint result);
}
