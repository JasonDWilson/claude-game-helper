using ClaudeGameHelper.Tray;

namespace ClaudeGameHelper.Tests.Tray;

public class TrayIconTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(32)]
    public void AppIconLoadsAtTraySizes(int size)
    {
        using var icon = TrayIcon.LoadAppIcon(size);

        Assert.Equal(size, icon.Width);
        Assert.Equal(size, icon.Height);
    }

    [Fact]
    public void DotIconKeepsSizeAndChangesTheTopRight()
    {
        using var plain = TrayIcon.LoadAppIcon(32);
        using var dotted = TrayIcon.WithDot(plain);
        using var a = plain.ToBitmap();
        using var b = dotted.ToBitmap();

        Assert.Equal(32, dotted.Width);
        Assert.NotEqual(a.GetPixel(25, 6), b.GetPixel(25, 6));
        Assert.Equal(a.GetPixel(8, 24), b.GetPixel(8, 24));
    }
}
