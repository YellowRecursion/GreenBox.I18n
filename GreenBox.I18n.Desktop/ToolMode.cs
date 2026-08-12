using GreenBox.I18n.Mcp;

namespace GreenBox.I18n.Desktop;

internal static class ToolMode
{
    internal static int Run(string mode, string[] arguments)
    {
        if (mode != "mcp")
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        using var host = new HostProcessManager();
        if (!host.EnsureStartedAsync().GetAwaiter().GetResult())
        {
            Console.Error.WriteLine("GreenBox I18n Host could not be started.");
            return 3;
        }

        Environment.SetEnvironmentVariable(
            "GREENBOX_I18N_HOST_URL",
            DesktopConstants.EditorUrl.TrimEnd('/'));
        McpApplication.RunAsync(arguments).GetAwaiter().GetResult();
        return 0;
    }
}
