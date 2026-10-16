using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ClaudeGameHelper.Platform;

namespace ClaudeGameHelper.Tests.Platform;

public class WindowInfoTests : IDisposable
{
    private readonly StaWindowHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void FromHandleDescribesOurOwnWindow()
    {
        var hwnd = _host.Invoke(() =>
        {
            var w = new Window { Title = "cgh test window", Width = 100, Height = 100, ShowActivated = false, ShowInTaskbar = false };
            w.Show();
            return new WindowInteropHelper(w).Handle;
        });

        var info = WindowInfo.FromHandle(hwnd);

        Assert.NotNull(info);
        Assert.Equal(hwnd, info.Hwnd);
        Assert.Equal(Environment.ProcessId, info.ProcessId);
        Assert.Equal(Process.GetCurrentProcess().ProcessName, info.ProcessName);
        Assert.Equal("cgh test window", info.Title);
    }

    [Fact]
    public void FromHandleOfNothingIsNull() => Assert.Null(WindowInfo.FromHandle(IntPtr.Zero));

    [Fact]
    public void SetNoActivateTogglesTheExtendedStyle()
    {
        const int GwlExStyle = -20;
        const long WsExNoActivate = 0x08000000;

        var (on, off) = _host.Invoke(() =>
        {
            var w = new Window { Width = 100, Height = 100, ShowActivated = false, ShowInTaskbar = false };
            w.Show();
            var hwnd = new WindowInteropHelper(w).Handle;
            FocusHelper.SetNoActivate(w, true);
            var styleOn = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
            FocusHelper.SetNoActivate(w, false);
            var styleOff = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
            return (styleOn, styleOff);
        });

        Assert.Equal(WsExNoActivate, on & WsExNoActivate);
        Assert.Equal(0, off & WsExNoActivate);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
}
