using ClaudeGameHelper.Platform;
using Microsoft.Win32;

namespace ClaudeGameHelper.Tests.Platform;

public class StartupRegistrationTests : IDisposable
{
    private readonly string _parent = $@"Software\ClaudeGameHelperTests\{Guid.NewGuid():N}";
    private string TestKey => _parent + @"\Run";

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(_parent, throwOnMissingSubKey: false);

    [Fact]
    public void RunKeyIsTheStandardOne() =>
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Run", StartupRegistration.RunKey);

    [Fact]
    public void EnableWritesQuotedPathAndDisableRemovesIt()
    {
        var s = new StartupRegistration(TestKey);
        Assert.False(s.IsEnabled);

        s.Enable(@"C:\Apps\Claude Game Helper\ClaudeGameHelper.exe");

        Assert.True(s.IsEnabled);
        using (var key = Registry.CurrentUser.OpenSubKey(TestKey)!)
            Assert.Equal("\"C:\\Apps\\Claude Game Helper\\ClaudeGameHelper.exe\"", key.GetValue("ClaudeGameHelper"));

        s.Disable();
        Assert.False(s.IsEnabled);
        s.Disable();
    }
}
