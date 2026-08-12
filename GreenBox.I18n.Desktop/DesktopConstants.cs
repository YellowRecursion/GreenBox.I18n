namespace GreenBox.I18n.Desktop;

internal static class DesktopConstants
{
    internal const string ProductName = "GreenBox I18n";
    internal const string InstanceMutexName = "Local\\GreenBox.I18n.Desktop";
    internal const string ActivationEventName = "Local\\GreenBox.I18n.Desktop.Activate";

    internal static string EditorUrl { get; } = GetEditorUrl();

    internal static string HealthUrl => EditorUrl + "api/health";

    private static string GetEditorUrl()
    {
        string url = Environment.GetEnvironmentVariable("GREENBOX_I18N_HOST_URL")
            ?? "http://127.0.0.1:5111";
        return url.TrimEnd('/') + "/";
    }
}
