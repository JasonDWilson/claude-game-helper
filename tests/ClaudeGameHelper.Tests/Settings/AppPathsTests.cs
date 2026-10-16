using ClaudeGameHelper.Core.Settings;

namespace ClaudeGameHelper.Tests.Settings;

public class AppPathsTests
{
    [Fact]
    public void DataRootDefaultsToDocuments() =>
        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Claude Game Helper"),
            AppPaths.DataRoot(null));

    [Fact]
    public void ConfiguredDataFolderWins() => Assert.Equal(@"C:\games\notes", AppPaths.DataRoot(@"C:\games\notes"));

    [Fact]
    public void BlankDataFolderMeansDefault() => Assert.Equal(AppPaths.DataRoot(null), AppPaths.DataRoot("  "));

    [Fact]
    public void LogsLiveUnderTheDataRoot() => Assert.Equal(@"C:\d\logs", AppPaths.LogsDir(@"C:\d"));

    [Fact]
    public void BuiltInProfilesShipNextToTheApp() =>
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "BuiltInProfiles"), AppPaths.BuiltInProfilesDir);

    [Fact]
    public void BuiltInProfilesAreCopiedToTheOutput() =>
        Assert.True(File.Exists(Path.Combine(AppPaths.BuiltInProfilesDir, "_template", "CLAUDE.md")));

    [Fact]
    public void SettingsLiveUnderAppData() =>
        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeGameHelper", "settings.json"),
            AppPaths.SettingsFile);
}
