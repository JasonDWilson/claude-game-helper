using ClaudeGameHelper.Core.Layout;
using ClaudeGameHelper.Core.Settings;

namespace ClaudeGameHelper.Tests.Layout;

public class PanelPlacementTests
{
    private static readonly RectD Main = new(0, 0, 2560, 1400);
    private static readonly RectD Second = new(2560, 0, 1920, 1040);

    [Fact]
    public void DefaultsToRightEdgeOfGameMonitor() =>
        Assert.Equal(new RectD(2120, 0, 440, 1400), PanelPlacement.Choose(null, [Main], Main));

    [Fact]
    public void DefaultFollowsTheGameMonitor() =>
        Assert.Equal(new RectD(4040, 0, 440, 1040), PanelPlacement.Choose(null, [Main, Second], Second));

    [Fact]
    public void SavedBoundsOnAMonitorAreKept() =>
        Assert.Equal(new RectD(3000, 50, 500, 900), PanelPlacement.Choose(new PanelBounds(3000, 50, 500, 900), [Main, Second], Main));

    [Fact]
    public void SavedBoundsOffEveryMonitorFallBackToDefault() =>
        Assert.Equal(new RectD(2120, 0, 440, 1400), PanelPlacement.Choose(new PanelBounds(3000, 50, 500, 900), [Main], Main));

    [Fact]
    public void SavedBoundsBarelyOnScreenFallBackToDefault() =>
        Assert.Equal(new RectD(2120, 0, 440, 1400), PanelPlacement.Choose(new PanelBounds(2550, 50, 500, 900), [Main], Main));
}
