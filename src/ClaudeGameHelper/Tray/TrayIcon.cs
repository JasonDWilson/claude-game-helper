using System.Drawing;
using Forms = System.Windows.Forms;

namespace ClaudeGameHelper.Tray;

/// <summary>The notification-area icon and its menu. The icon is the app's own app.ico, embedded in the exe.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Icon _image;
    private readonly Forms.ToolStripMenuItem _openTracking;
    private readonly Func<(string Label, bool Enabled)> _trackingState;

    public TrayIcon(Action showPanel, Action newConversation, Action openTracking, Func<(string Label, bool Enabled)> trackingState,
        Action openSettings, Func<bool> startWithWindows, Action<bool> setStartWithWindows, string versionText, Action quit)
    {
        _trackingState = trackingState;
        _image = LoadAppIcon(Forms.SystemInformation.SmallIconSize.Width);

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(new Forms.ToolStripMenuItem($"Claude Game Helper {versionText}") { Enabled = false });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Show panel", null, (_, _) => showPanel());
        menu.Items.Add("New conversation", null, (_, _) => newConversation());
        _openTracking = new Forms.ToolStripMenuItem("Open tracking file", null, (_, _) => openTracking());
        menu.Items.Add(_openTracking);
        menu.Items.Add("Settings", null, (_, _) => openSettings());
        var startup = new Forms.ToolStripMenuItem("Start with Windows") { CheckOnClick = true };
        startup.Click += (_, _) => setStartWithWindows(startup.Checked);
        menu.Items.Add(startup);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => quit());
        menu.Opening += (_, _) =>
        {
            var (label, enabled) = _trackingState();
            _openTracking.Text = $"Open {label.ToLowerInvariant()} file";
            _openTracking.Enabled = enabled;
            startup.Checked = startWithWindows();
        };

        _icon = new Forms.NotifyIcon
        {
            Icon = _image,
            Text = "Claude Game Helper",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => showPanel();
    }

    public void Balloon(string text) =>
        _icon.ShowBalloonTip(5000, "Claude Game Helper", text, Forms.ToolTipIcon.None);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _image.Dispose();
    }

    /// <summary>The app icon (app.ico, embedded) at the closest size to <paramref name="size"/> pixels.</summary>
    public static Icon LoadAppIcon(int size)
    {
        using var stream = typeof(TrayIcon).Assembly.GetManifestResourceStream(AppIconResource)
            ?? throw new InvalidOperationException($"Embedded resource {AppIconResource} is missing.");
        return new Icon(stream, size, size);
    }

    private const string AppIconResource = "ClaudeGameHelper.app.ico";
}
