using ClaudeGameHelper.Core.Layout;

namespace ClaudeGameHelper.Tests.Layout;

public class ScreenshotSizingTests
{
    [Theory]
    [InlineData(2560, 1440, 1568, 1568, 882)]
    [InlineData(1440, 2560, 1568, 882, 1568)]
    [InlineData(800, 600, 1568, 800, 600)]
    [InlineData(1568, 1000, 1568, 1568, 1000)]
    public void Fits(int w, int h, int max, int ew, int eh) =>
        Assert.Equal((ew, eh), ScreenshotSizing.Fit(w, h, max));
}
