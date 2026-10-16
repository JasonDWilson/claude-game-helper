using ClaudeGameHelper.Core.Settings;

namespace ClaudeGameHelper.Core.Layout;

public sealed record RectD(double X, double Y, double Width, double Height);

public static class PanelPlacement
{
    public const double DefaultWidth = 440;
    private const double MinVisible = 100;

    /// <summary>
    /// Where the panel goes: its saved bounds if they're still mostly on a monitor, otherwise docked to the
    /// right edge of the game's monitor, full height.
    /// </summary>
    public static RectD Choose(PanelBounds? saved, IReadOnlyList<RectD> workAreas, RectD gameWorkArea)
    {
        if (saved != null)
        {
            var bounds = new RectD(saved.X, saved.Y, saved.Width, saved.Height);
            if (workAreas.Any(area => Overlap(bounds.X, bounds.Width, area.X, area.Width) >= MinVisible
                                   && Overlap(bounds.Y, bounds.Height, area.Y, area.Height) >= MinVisible))
                return bounds;
        }

        return new RectD(gameWorkArea.X + gameWorkArea.Width - DefaultWidth, gameWorkArea.Y, DefaultWidth, gameWorkArea.Height);
    }

    private static double Overlap(double start1, double length1, double start2, double length2) =>
        Math.Min(start1 + length1, start2 + length2) - Math.Max(start1, start2);
}
