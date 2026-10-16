using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace ClaudeGameHelper.Tray;

/// <summary>The notification-area icon and its menu. The icon is drawn at runtime; there are no image files.</summary>
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
        _image = DrawIcon();

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
        DestroyIcon(_image.Handle);
        _image.Dispose();
    }

    private static Icon DrawIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var orange = new SolidBrush(Color.FromArgb(0xD9, 0x77, 0x57));
            g.FillEllipse(orange, 1, 1, 30, 30);
            using var font = new Font("Segoe UI", 15, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
            using var dark = new SolidBrush(Color.FromArgb(0x1E, 0x1F, 0x22));
            var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("C", font, dark, new RectangleF(0, 1, 32, 32), format);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
