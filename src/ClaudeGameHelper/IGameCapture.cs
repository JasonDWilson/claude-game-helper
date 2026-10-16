namespace ClaudeGameHelper;

public sealed class CaptureFailedException(string message) : Exception(message);

public interface IGameCapture
{
    /// <summary>Captures the window into a .jpg in <paramref name="screenshotsDir"/> and returns its path.</summary>
    Task<string> CaptureAsync(IntPtr hwnd, string screenshotsDir);
}
