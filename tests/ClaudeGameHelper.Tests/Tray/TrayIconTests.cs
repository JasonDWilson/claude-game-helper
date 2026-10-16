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
}
