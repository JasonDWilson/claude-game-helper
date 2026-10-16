using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClaudeGameHelper.Capture;
using ClaudeGameHelper.Core.Settings;

namespace ClaudeGameHelper.Tests.Capture;

public class WindowCaptureTests : IDisposable
{
    private readonly StaWindowHost _host = new();
    private readonly string _dir = TestPaths.NewTempDir();

    public void Dispose() => _host.Dispose();

    private IntPtr ShowWindow(Brush background, bool topmost = false, WindowState state = WindowState.Normal) => _host.Invoke(() =>
    {
        var w = new Window
        {
            Width = 400, Height = 300, Left = 100, Top = 100,
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            Background = background, Topmost = topmost, ShowActivated = false, ShowInTaskbar = false,
        };
        w.Show();
        w.WindowState = state;
        return new WindowInteropHelper(w).Handle;
    });

    private static WindowCapture Capture(int maxLongEdge = 200) =>
        new(() => new AppSettings { ScreenshotMaxLongEdge = maxLongEdge });

    [Fact]
    public async Task CapturesAnOccludedRedWindow()
    {
        var red = ShowWindow(Brushes.Red);
        ShowWindow(Brushes.White, topmost: true);
        await Task.Delay(500);

        var path = await Capture().CaptureAsync(red, _dir);

        Assert.Equal(_dir, Path.GetDirectoryName(path));
        Assert.Matches(new Regex(@"^\d{8}-\d{6}-\d{3}\.jpg$"), Path.GetFileName(path));
        var bitmap = new FormatConvertedBitmap(BitmapFrame.Create(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.OnLoad), PixelFormats.Bgra32, null, 0);
        Assert.Equal(200, Math.Max(bitmap.PixelWidth, bitmap.PixelHeight));
        var pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth / 2, bitmap.PixelHeight / 2, 1, 1), pixel, 4, 0);
        Assert.True(pixel[2] > 200 && pixel[1] < 60 && pixel[0] < 60, $"center pixel B={pixel[0]} G={pixel[1]} R={pixel[2]}");
    }

    [Fact]
    public async Task GraphicsFailureIsACaptureFailureAndTheDeviceIsRecreated()
    {
        var red = ShowWindow(Brushes.Red);
        await Task.Delay(300);
        var calls = 0;
        var capture = new WindowCapture(() => new AppSettings { ScreenshotMaxLongEdge = 200 },
            () => ++calls == 1 ? throw new System.Runtime.InteropServices.COMException("device removed") : WindowCapture.CreateDefaultDevice());

        var ex = await Assert.ThrowsAsync<CaptureFailedException>(() => capture.CaptureAsync(red, _dir));
        var path = await capture.CaptureAsync(red, _dir);

        Assert.StartsWith("Windows couldn't capture the game window", ex.Message);
        Assert.Equal(2, calls);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task MinimizedWindowThrowsCaptureFailed()
    {
        var hwnd = ShowWindow(Brushes.Red, state: WindowState.Minimized);

        var ex = await Assert.ThrowsAsync<CaptureFailedException>(() => Capture().CaptureAsync(hwnd, _dir));

        Assert.Equal("The game window is minimized.", ex.Message);
    }

    [Fact]
    public async Task ClosedWindowThrowsCaptureFailed()
    {
        var hwnd = _host.Invoke(() =>
        {
            var w = new Window { Width = 100, Height = 100, ShowActivated = false, ShowInTaskbar = false };
            w.Show();
            var handle = new WindowInteropHelper(w).Handle;
            w.Close();
            return handle;
        });

        var ex = await Assert.ThrowsAsync<CaptureFailedException>(() => Capture().CaptureAsync(hwnd, _dir));

        Assert.Equal("The game window is closed.", ex.Message);
    }
}
