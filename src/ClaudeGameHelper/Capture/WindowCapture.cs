using System.Runtime.InteropServices;
using ClaudeGameHelper.Core;
using ClaudeGameHelper.Core.Layout;
using ClaudeGameHelper.Core.Settings;
using Windows.Foundation;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;

namespace ClaudeGameHelper.Capture;

/// <summary>
/// Captures one window with Windows.Graphics.Capture. It reads the window's own content, so it works for
/// DirectX borderless games and ignores anything drawn over the window (such as the panel).
/// </summary>
public sealed class WindowCapture(Func<AppSettings> settings, Func<IDirect3DDevice>? createDevice = null) : IGameCapture
{
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(2);
    private static bool _borderlessRequested;
    private readonly Func<IDirect3DDevice> _createDevice = createDevice ?? CreateDefaultDevice;
    private IDirect3DDevice? _device;

    public static IDirect3DDevice CreateDefaultDevice() => WgcInterop.CreateDevice();

    public async Task<string> CaptureAsync(IntPtr hwnd, string screenshotsDir)
    {
        if (!IsWindow(hwnd))
            throw new CaptureFailedException("The game window is closed.");
        if (IsIconic(hwnd))
            throw new CaptureFailedException("The game window is minimized.");
        if (!GraphicsCaptureSession.IsSupported())
            throw new CaptureFailedException("Windows screen capture isn't available on this PC.");

        await RequestBorderlessOnceAsync();
        SoftwareBitmap grabbed;
        try
        {
            _device ??= _createDevice();
            grabbed = await GrabFrameAsync(hwnd, _device);
        }
        catch (COMException ex)
        {
            // A lost GPU device (driver reset mid-game) is useless from now on; build a new one next time.
            _device = null;
            Log.Info($"Capture failed: {ex.Message}");
            throw new CaptureFailedException($"Windows couldn't capture the game window ({ex.Message}).");
        }

        using var bitmap = grabbed;
        var current = settings();
        var (width, height) = ScreenshotSizing.Fit(bitmap.PixelWidth, bitmap.PixelHeight, current.ScreenshotMaxLongEdge);

        Directory.CreateDirectory(screenshotsDir);
        var path = Path.Combine(screenshotsDir, $"{DateTime.Now:yyyyMMdd-HHmmss-fff}.jpg");
        await SaveJpegAsync(bitmap, path, width, height, current.ScreenshotJpegQuality);
        return path;
    }

    private static async Task<SoftwareBitmap> GrabFrameAsync(IntPtr hwnd, IDirect3DDevice device)
    {
        GraphicsCaptureItem item;
        try
        {
            item = WgcInterop.CreateItemForWindow(hwnd);
        }
        catch (Exception ex) when (ex is COMException or ArgumentException)
        {
            throw new CaptureFailedException($"Windows wouldn't capture that window ({ex.Message}).");
        }

        using var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, item.Size);
        using var session = pool.CreateCaptureSession(item);
        session.IsCursorCaptureEnabled = false;
        try
        {
            session.IsBorderRequired = false;
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException)
        {
            // Without permission Windows briefly draws a yellow border; the picture is still fine.
        }

        var frameArrived = new TaskCompletionSource<Direct3D11CaptureFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        pool.FrameArrived += (p, _) =>
        {
            var frame = p.TryGetNextFrame();
            if (frame != null && !frameArrived.TrySetResult(frame))
                frame.Dispose();
        };
        session.StartCapture();

        if (await Task.WhenAny(frameArrived.Task, Task.Delay(FrameTimeout)) != frameArrived.Task)
            throw new CaptureFailedException("The game window didn't produce a picture.");

        using var captured = await frameArrived.Task;
        using var copy = await SoftwareBitmap.CreateCopyFromSurfaceAsync(captured.Surface, BitmapAlphaMode.Premultiplied);
        return SoftwareBitmap.Convert(copy, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
    }

    private static async Task SaveJpegAsync(SoftwareBitmap bitmap, string path, int width, int height, int quality)
    {
        using var file = File.Create(path);
        using var stream = file.AsRandomAccessStream();
        var properties = new BitmapPropertySet
        {
            { "ImageQuality", new BitmapTypedValue(quality / 100f, PropertyType.Single) },
        };
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream, properties);
        encoder.SetSoftwareBitmap(bitmap);
        encoder.BitmapTransform.ScaledWidth = (uint)width;
        encoder.BitmapTransform.ScaledHeight = (uint)height;
        encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
        await encoder.FlushAsync();
    }

    private static async Task RequestBorderlessOnceAsync()
    {
        if (_borderlessRequested)
            return;
        _borderlessRequested = true;
        try
        {
            var status = await GraphicsCaptureAccess.RequestAccessAsync(GraphicsCaptureAccessKind.Borderless);
            Log.Info($"Borderless capture access: {status}.");
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException)
        {
            Log.Info($"Borderless capture access request failed: {ex.Message}");
        }
    }

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hwnd);
}
