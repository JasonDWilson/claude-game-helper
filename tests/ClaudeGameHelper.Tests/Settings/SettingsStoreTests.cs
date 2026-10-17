using ClaudeGameHelper.Core.Settings;

namespace ClaudeGameHelper.Tests.Settings;

public class SettingsStoreTests
{
    private readonly string _path = Path.Combine(TestPaths.NewTempDir(), "settings.json");

    [Fact]
    public void MissingFileIsCreatedWithDefaults()
    {
        var r = new SettingsStore(_path).Load();

        Assert.Null(r.Error);
        Assert.Equal("Ctrl+Shift+F1", r.Settings.ScreenHotkey);
        Assert.Equal("Ctrl+Shift+F2", r.Settings.AskHotkey);
        Assert.Equal("Ctrl+Shift+F3", r.Settings.ToggleHotkey);
        Assert.Equal("sonnet", r.Settings.FastModel);
        Assert.Equal("low", r.Settings.FastEffort);
        Assert.Equal("opus", r.Settings.DeepModel);
        Assert.Equal("medium", r.Settings.DeepEffort);
        Assert.Equal(100_000, r.Settings.RolloverContextTokens);
        Assert.Equal(1568, r.Settings.ScreenshotMaxLongEdge);
        Assert.Equal(90, r.Settings.ScreenshotJpegQuality);
        Assert.True(File.Exists(_path));
        Assert.Contains("\"screenHotkey\"", File.ReadAllText(_path));
    }

    [Fact]
    public void SavedFileIsReadableByHand()
    {
        new SettingsStore(_path).Load();

        Assert.Contains("\"screenHotkey\": \"Ctrl+Shift+F1\"", File.ReadAllText(_path));
    }

    [Fact]
    public void BrokenFileFallsBackToDefaultsAndIsNotOverwritten()
    {
        const string broken = "{ \"fastModel\": \"haiku\", oops }";
        File.WriteAllText(_path, broken);

        var r = new SettingsStore(_path).Load();

        Assert.NotNull(r.Error);
        Assert.Contains("settings.json", r.Error);
        Assert.Equal("sonnet", r.Settings.FastModel);
        Assert.Equal(broken, File.ReadAllText(_path));
    }

    [Fact]
    public void V1SettingsWithProfilesFolderStillLoad()
    {
        File.WriteAllText(_path, "{ \"profilesFolder\": \"C:\\\\x\", \"fastModel\": \"haiku\" }");

        var r = new SettingsStore(_path).Load();

        Assert.Null(r.Error);
        Assert.Equal("haiku", r.Settings.FastModel);
    }

    [Fact]
    public void SavingDropsTheRetiredProfilesFolderKey()
    {
        File.WriteAllText(_path, "{ \"profilesFolder\": null }");
        var store = new SettingsStore(_path);

        store.Save(store.Load().Settings);

        Assert.DoesNotContain("profilesFolder", File.ReadAllText(_path));
    }

    [Fact]
    public void NotGamesAndDataFolderRoundTrip()
    {
        var store = new SettingsStore(_path);

        store.Save(new AppSettings { NotGames = ["launcher"], DataFolder = @"C:\d" });
        var loaded = store.Load().Settings;

        Assert.Equal(["launcher"], loaded.NotGames);
        Assert.Equal(@"C:\d", loaded.DataFolder);
    }

    [Fact]
    public void NotGamesDefaultsToEmpty() => Assert.Empty(new AppSettings().NotGames);

    [Fact]
    public void WatchSettingsDefaultsAndRoundTrip()
    {
        var defaults = new AppSettings().Watch;
        Assert.Equal(30, defaults.CheckEverySeconds);
        Assert.True(defaults.Chime);
        Assert.Equal(new ClaudeGameHelper.Core.Watch.WatchSwitches(false, false), defaults.For("anything"));
        Assert.Equal([20, 30, 60, 120], ClaudeGameHelper.Core.Watch.WatchSettings.Frequencies);

        var store = new SettingsStore(_path);
        store.Save(new AppSettings
        {
            Watch = new ClaudeGameHelper.Core.Watch.WatchSettings
            {
                CheckEverySeconds = 60,
                Chime = false,
                Games = new Dictionary<string, ClaudeGameHelper.Core.Watch.WatchSwitches> { ["fallout-76"] = new(true, false) },
            },
        });
        var loaded = store.Load();

        Assert.Null(loaded.Error);
        Assert.Equal(60, loaded.Settings.Watch.CheckEverySeconds);
        Assert.False(loaded.Settings.Watch.Chime);
        Assert.Equal(new ClaudeGameHelper.Core.Watch.WatchSwitches(true, false), loaded.Settings.Watch.For("fallout-76"));
    }

    [Fact]
    public void MisspelledKeyIsReportedByName()
    {
        const string typo = "{ \"fastModle\": \"haiku\" }";
        File.WriteAllText(_path, typo);

        var r = new SettingsStore(_path).Load();

        Assert.NotNull(r.Error);
        Assert.Contains("fastModle", r.Error);
        Assert.Equal("sonnet", r.Settings.FastModel);
        Assert.Equal(typo, File.ReadAllText(_path));
    }

    [Fact]
    public void CommentsAndTrailingCommasAreAccepted()
    {
        File.WriteAllText(_path, "{ // mine\n \"fastModel\": \"haiku\", }");

        var r = new SettingsStore(_path).Load();

        Assert.Null(r.Error);
        Assert.Equal("haiku", r.Settings.FastModel);
        Assert.Equal("Ctrl+Shift+F1", r.Settings.ScreenHotkey);
    }

    [Fact]
    public void SaveRoundTripsPanelBounds()
    {
        var store = new SettingsStore(_path);
        store.Save(new AppSettings { Panel = new PanelBounds(10, 20, 440, 900) });

        Assert.Equal(new PanelBounds(10, 20, 440, 900), store.Load().Settings.Panel);
    }
}
