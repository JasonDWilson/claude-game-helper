namespace ClaudeGameHelper;

public sealed class CaptureFailedException(string message) : Exception(message);

/// <summary>A watch-mode capture: the saved picture plus a tiny grayscale thumbnail for the change check.</summary>
public sealed record WatchFrame(string Path, byte[] Thumbnail);

public interface IGameCapture
{
    /// <summary>Captures the window into a .jpg in <paramref name="screenshotsDir"/> and returns its path.</summary>
    Task<string> CaptureAsync(IntPtr hwnd, string screenshotsDir);

    /// <summary>Captures the window into <paramref name="jpgPath"/> (overwriting it) for a watch check.</summary>
    Task<WatchFrame> CaptureForWatchAsync(IntPtr hwnd, string jpgPath);
}
