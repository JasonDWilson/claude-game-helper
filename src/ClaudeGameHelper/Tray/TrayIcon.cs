using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using ClaudeGameHelper.Core.Watch;
using Forms = System.Windows.Forms;

namespace ClaudeGameHelper.Tray;

/// <summary>The notification-area icon and its menu. The icon is the app's own app.ico, embedded in the exe.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Icon _image;
    private readonly Icon _imageWithDot;
    private readonly Forms.ToolStripMenuItem _openTracking;
    private readonly Func<(string Label, bool Enabled)> _trackingState;

    public TrayIcon(Action showPanel, Action newConversation, Action openTracking, Func<(string Label, bool Enabled)> trackingState,
        Action openSettings, Func<bool> startWithWindows, Action<bool> setStartWithWindows, string versionText,
        IWatchMenu watchMenu, Action quit)
    {
        _trackingState = trackingState;
        _image = LoadAppIcon(Forms.SystemInformation.SmallIconSize.Width);
        _imageWithDot = WithDot(_image);

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(new Forms.ToolStripMenuItem($"Claude Game Helper {versionText}") { Enabled = false });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Show panel", null, (_, _) => showPanel());
        menu.Items.Add("New conversation", null, (_, _) => newConversation());
        _openTracking = new Forms.ToolStripMenuItem("Open tracking file", null, (_, _) => openTracking());
        menu.Items.Add(_openTracking);
        var watch = new Forms.ToolStripMenuItem("Watch mode");
        menu.Items.Add(watch);
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
            BuildWatchMenu(watch, watchMenu);
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

    /// <summary>Shows or clears the dot that means "watch mode left you a tip".</summary>
    public void SetAttention(bool on) => _icon.Icon = on ? _imageWithDot : _image;

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _image.Dispose();
        _imageWithDot.Dispose();
    }

    /// <summary>The app icon (app.ico, embedded) at the closest size to <paramref name="size"/> pixels.</summary>
    public static Icon LoadAppIcon(int size)
    {
        using var stream = typeof(TrayIcon).Assembly.GetManifestResourceStream(AppIconResource)
            ?? throw new InvalidOperationException($"Embedded resource {AppIconResource} is missing.");
        return new Icon(stream, size, size);
    }

    /// <summary>The icon with a cream dot (dark ring) in its top-right corner.</summary>
    public static Icon WithDot(Icon icon)
    {
        using var bitmap = icon.ToBitmap();
        var size = bitmap.Width;
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var radius = size * 0.2f;
            var rect = new RectangleF(size * 0.78f - radius, size * 0.22f - radius, radius * 2, radius * 2);
            using var cream = new SolidBrush(Color.FromArgb(0xFF, 0xF4, 0xE8));
            using var ring = new Pen(Color.FromArgb(0x1E, 0x1F, 0x22), Math.Max(1f, size / 16f));
            g.FillEllipse(cream, rect);
            g.DrawEllipse(ring, rect);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static void BuildWatchMenu(Forms.ToolStripMenuItem root, IWatchMenu watch)
    {
        root.DropDownItems.Clear();
        root.DropDownItems.Add(new Forms.ToolStripMenuItem(watch.GameName is { } name ? $"For {name}" : "No game in front") { Enabled = false });

        var follow = new Forms.ToolStripMenuItem("Follow my goal") { Checked = watch.FollowGoal, Enabled = watch.Allowed };
        follow.Click += (_, _) => watch.FollowGoal = !watch.FollowGoal;
        var tips = new Forms.ToolStripMenuItem("General tips") { Checked = watch.GeneralTips, Enabled = watch.Allowed };
        tips.Click += (_, _) => watch.GeneralTips = !watch.GeneralTips;
        root.DropDownItems.Add(follow);
        root.DropDownItems.Add(tips);
        root.DropDownItems.Add(new Forms.ToolStripSeparator());

        foreach (var seconds in WatchSettings.Frequencies)
        {
            var label = seconds < 60 ? $"Check every {seconds} s" : $"Check every {seconds / 60} min";
            var item = new Forms.ToolStripMenuItem(label) { Checked = watch.CheckEverySeconds == seconds };
            item.Click += (_, _) => watch.CheckEverySeconds = seconds;
            root.DropDownItems.Add(item);
        }

        root.DropDownItems.Add(new Forms.ToolStripSeparator());
        var chime = new Forms.ToolStripMenuItem("Chime") { Checked = watch.Chime };
        chime.Click += (_, _) => watch.Chime = !watch.Chime;
        root.DropDownItems.Add(chime);
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private const string AppIconResource = "ClaudeGameHelper.app.ico";
}
