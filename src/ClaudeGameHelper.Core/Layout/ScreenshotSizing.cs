namespace ClaudeGameHelper.Core.Layout;

public static class ScreenshotSizing
{
    /// <summary>Scales a size down so its long edge is at most <paramref name="maxLongEdge"/>; never scales up.</summary>
    public static (int Width, int Height) Fit(int width, int height, int maxLongEdge)
    {
        var longEdge = Math.Max(width, height);
        if (longEdge <= maxLongEdge)
            return (width, height);

        var scale = (double)maxLongEdge / longEdge;
        return width >= height
            ? (maxLongEdge, Math.Max(1, (int)Math.Round(height * scale)))
            : (Math.Max(1, (int)Math.Round(width * scale)), maxLongEdge);
    }
}
