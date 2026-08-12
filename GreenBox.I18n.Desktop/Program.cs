using Velopack;
using GreenBox.I18n.Editor.Host;

namespace GreenBox.I18n.Desktop;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .OnAfterInstallFastCallback(_ => CommandLineIntegration.Install())
            .OnBeforeUninstallFastCallback(_ => CommandLineIntegration.Uninstall())
            .Run();

        if (args.Length > 0 && args[0] == "host")
        {
            EditorHostApplication.RunAsync(args[1..]).GetAwaiter().GetResult();
            return 0;
        }

        if (args.Length > 0 && args[0] == "mcp")
        {
            return ToolMode.Run(args[0], args[1..]);
        }

        using var instanceMutex = new Mutex(
            initiallyOwned: true,
            DesktopConstants.InstanceMutexName,
            out bool isFirstInstance);
        using var activationEvent = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            DesktopConstants.ActivationEventName);

        if (!isFirstInstance)
        {
            activationEvent.Set();
            return 0;
        }

        ApplicationConfiguration.Initialize();
        using var context = new DesktopApplicationContext(activationEvent);
        Application.Run(context);
        return 0;
    }
}
