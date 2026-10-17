namespace ClaudeGameHelper.Core.Watch;

/// <summary>
/// Compares tiny grayscale thumbnails of the game so watch mode can skip frames that barely changed
/// (standing still, sitting in a menu) without asking Claude.
/// </summary>
public static class FrameDiff
{
    public const int Width = 64;
    public const int Height = 36;

    /// <summary>Frames that differ by less than this are treated as unchanged.</summary>
    public const double SkipBelow = 0.03;

    /// <summary>Mean absolute difference, 0 (same) to 1 (black vs. white). No previous frame or a size mismatch counts as 1.</summary>
    public static double Difference(byte[]? previous, byte[] current)
    {
        if (previous == null || previous.Length != current.Length || current.Length == 0)
            return 1;

        long total = 0;
        for (var i = 0; i < current.Length; i++)
            total += Math.Abs(previous[i] - current[i]);
        return total / (255.0 * current.Length);
    }
}
