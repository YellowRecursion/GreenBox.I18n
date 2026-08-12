using System.Drawing;

namespace GreenBox.I18n.Desktop;

internal static class DesktopBranding
{
    internal static Icon Icon { get; } =
        Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
}
