using System.Diagnostics;
using System.Drawing;

namespace GreenBox.I18n.Desktop;

internal sealed class DesktopApplicationContext : ApplicationContext
{
    private readonly DesktopForm _form = new();
    private readonly HostProcessManager _host = new();
    private readonly DesktopUpdateService _updates = new();
    private readonly NotifyIcon _trayIcon;
    private readonly EventWaitHandle _activationEvent;
    private readonly RegisteredWaitHandle _activationWait;
    private bool _exiting;

    internal DesktopApplicationContext(EventWaitHandle activationEvent)
    {
        _activationEvent = activationEvent;
        _host.LogReceived += _form.AppendLog;
        _updates.LogReceived += _form.AppendLog;
        _updates.UpdateReady += _form.SetUpdateReady;

        _form.OpenEditorRequested += OpenEditor;
        _form.RestartHostRequested += RestartHost;
        _form.ApplyUpdateRequested += ApplyUpdateAndRestart;
        _form.FormClosing += HideInsteadOfClose;
        _form.Resize += HideWhenMinimized;
        _form.Shown += Start;

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Open Editor", null, (_, _) => OpenEditor());
        trayMenu.Items.Add("Show Console", null, (_, _) => ShowConsole());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = $"{DesktopConstants.ProductName} Beta",
            ContextMenuStrip = trayMenu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => ShowConsole();

        _activationWait = ThreadPool.RegisterWaitForSingleObject(
            activationEvent,
            (_, _) => _form.BeginInvoke(ShowConsole),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        MainForm = _form;
        _form.Show();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _activationWait.Unregister(null);
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _host.Dispose();
            _form.Dispose();
        }

        base.Dispose(disposing);
    }

    private async void Start(object? sender, EventArgs args)
    {
        _form.AppendLog($"GreenBox I18n {ProductVersion.Current} started.");
        bool ready = await _host.EnsureStartedAsync();
        _form.SetHostReady(ready);
        if (ready)
        {
            OpenEditor();
        }

        _ = _updates.CheckAndDownloadAsync();
    }

    private void OpenEditor()
    {
        try
        {
            _form.OpenEditor();
        }
        catch (Exception exception)
        {
            _form.AppendLog($"Could not open the browser: {exception.Message}");
        }
    }

    private async void RestartHost()
    {
        _form.AppendLog("Restarting Host...");
        bool ready = await _host.RestartAsync();
        _form.SetHostReady(ready);
    }

    private void ApplyUpdateAndRestart()
    {
        _exiting = true;
        _host.StopOwnedHost();
        if (!_updates.ApplyAndRestart())
        {
            _exiting = false;
        }
    }

    private void ExitApplication()
    {
        _exiting = true;
        _trayIcon.Visible = false;
        _host.StopOwnedHost();
        if (_updates.HasPendingUpdate && _updates.ApplyAndExit())
        {
            return;
        }

        _form.Close();
        ExitThread();
    }

    private void HideInsteadOfClose(object? sender, FormClosingEventArgs args)
    {
        if (_exiting)
        {
            return;
        }

        args.Cancel = true;
        _form.Hide();
        _trayIcon.ShowBalloonTip(
            1500,
            DesktopConstants.ProductName,
            "GreenBox I18n is still running. Use Exit in the tray menu to stop it.",
            ToolTipIcon.Info);
    }

    private void HideWhenMinimized(object? sender, EventArgs args)
    {
        if (_form.WindowState == FormWindowState.Minimized)
        {
            _form.Hide();
        }
    }

    private void ShowConsole()
    {
        if (_form.IsDisposed)
        {
            return;
        }

        _form.Show();
        _form.WindowState = FormWindowState.Normal;
        _form.Activate();
    }
}
