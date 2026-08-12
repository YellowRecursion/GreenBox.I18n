using System.Diagnostics;
using System.Drawing;

namespace GreenBox.I18n.Desktop;

internal sealed class DesktopForm : Form
{
    private readonly Label _statusLabel;
    private readonly RichTextBox _log;
    private readonly Button _checkUpdatesButton;
    private readonly Button _updateButton;

    internal event Action? OpenEditorRequested;
    internal event Action? RestartHostRequested;
    internal event Action? CheckUpdatesRequested;
    internal event Action? ApplyUpdateRequested;

    internal DesktopForm()
    {
        Text = $"{DesktopConstants.ProductName} Beta";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(720, 440);
        Size = new Size(860, 540);
        BackColor = Color.FromArgb(18, 18, 18);
        ForeColor = Color.FromArgb(230, 230, 230);
        Font = new Font("Segoe UI", 10F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 16),
        };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        heading.Controls.Add(new Label
        {
            AutoSize = true,
            Text = DesktopConstants.ProductName,
            Font = new Font("Segoe UI Semibold", 16F),
            ForeColor = Color.White,
        }, 0, 0);
        heading.Controls.Add(new Label
        {
            AutoSize = true,
            Text = GreenBox.I18n.Desktop.ProductVersion.Current,
            ForeColor = Color.FromArgb(145, 145, 145),
            Anchor = AnchorStyles.Right,
        }, 1, 0);

        var controls = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 14),
        };
        controls.Controls.Add(CreateButton("Open Editor", () => OpenEditorRequested?.Invoke(), primary: true));
        controls.Controls.Add(CreateButton("Copy URL", CopyEditorUrl));
        controls.Controls.Add(CreateButton("Restart Host", () => RestartHostRequested?.Invoke()));
        _checkUpdatesButton = CreateButton("Check for updates", () => CheckUpdatesRequested?.Invoke());
        controls.Controls.Add(_checkUpdatesButton);

        _updateButton = CreateButton("Restart to update", () => ApplyUpdateRequested?.Invoke(), primary: true);
        _updateButton.Visible = false;
        controls.Controls.Add(_updateButton);

        _statusLabel = new Label
        {
            AutoSize = true,
            Text = "● Starting Host",
            ForeColor = Color.FromArgb(216, 164, 54),
            Padding = new Padding(10, 8, 0, 0),
        };
        controls.Controls.Add(_statusLabel);

        _log = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(10, 10, 10),
            ForeColor = Color.FromArgb(205, 205, 205),
            Font = new Font("Cascadia Mono", 9.5F),
            DetectUrls = false,
            WordWrap = false,
            Margin = new Padding(0),
        };

        root.Controls.Add(heading, 0, 0);
        root.Controls.Add(controls, 0, 1);
        root.Controls.Add(_log, 0, 2);
        Controls.Add(root);
    }

    internal void SetHostReady(bool ready)
    {
        RunOnUiThread(() =>
        {
            _statusLabel.Text = ready ? "● Host online" : "● Host unavailable";
            _statusLabel.ForeColor = ready
                ? Color.FromArgb(82, 196, 26)
                : Color.FromArgb(255, 77, 79);
        });
    }

    internal void SetUpdateReady(string version)
    {
        RunOnUiThread(() =>
        {
            _updateButton.Text = $"Restart to update {version}";
            _updateButton.Visible = true;
        });
    }

    internal void SetUpdateCheckRunning(bool running)
    {
        RunOnUiThread(() =>
        {
            _checkUpdatesButton.Enabled = !running;
            _checkUpdatesButton.Text = running ? "Checking..." : "Check for updates";
        });
    }

    internal void AppendLog(string message)
    {
        RunOnUiThread(() =>
        {
            _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
        });
    }

    internal void OpenEditor()
    {
        Process.Start(new ProcessStartInfo(DesktopConstants.EditorUrl)
        {
            UseShellExecute = true,
        });
    }

    private static Button CreateButton(string text, Action onClick, bool primary = false)
    {
        var button = new Button
        {
            AutoSize = true,
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Color.FromArgb(22, 119, 255) : Color.FromArgb(42, 42, 42),
            ForeColor = Color.White,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(0, 0, 8, 0),
            Cursor = Cursors.Hand,
        };
        button.FlatAppearance.BorderColor = primary
            ? Color.FromArgb(22, 119, 255)
            : Color.FromArgb(70, 70, 70);
        button.Click += (_, _) => onClick();
        return button;
    }

    private void CopyEditorUrl()
    {
        Clipboard.SetText(DesktopConstants.EditorUrl);
        AppendLog("Editor URL copied to the clipboard.");
    }

    private void RunOnUiThread(Action action)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(action);
        }
        else
        {
            action();
        }
    }
}
