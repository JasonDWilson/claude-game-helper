using ClaudeGameHelper.Core.Profiles;

namespace ClaudeGameHelper.Tests.Profiles;

public class AutoProfilesTests
{
    private readonly string _data = TestPaths.NewTempDir();
    private static readonly string Template = Path.Combine(TestPaths.BuiltInProfilesRoot, "_template", "CLAUDE.md");

    [Theory]
    [InlineData("valheim.exe", "Valheim", "valheim")]
    [InlineData("Cyberpunk2077", "Cyberpunk 2077 (C) 2020 by CD Projekt RED", "cyberpunk2077")]
    [InlineData("javaw", "Minecraft* 1.21: Tricky Trials", "minecraft-1-21-tricky-trials")]
    public void KeysComeFromProcessOrForJavaFromTitle(string process, string title, string key) =>
        Assert.Equal(key, AutoProfiles.KeyFor(process, title));

    [Theory]
    [InlineData("explorer")]
    [InlineData("chrome")]
    [InlineData("msedge")]
    [InlineData("firefox")]
    [InlineData("SearchHost")]
    [InlineData("ShellExperienceHost")]
    [InlineData("")]
    public void ShellAndBrowsersNeverGetProfiles(string process) => Assert.Null(AutoProfiles.KeyFor(process, "x"));

    [Fact]
    public void LongJavaTitlesAreCappedAt40Chars()
    {
        var key = AutoProfiles.KeyFor("javaw", new string('a', 30) + " " + new string('b', 200))!;

        Assert.True(key.Length <= 40);
        Assert.False(key.EndsWith('-'));
    }

    [Fact]
    public void StoreGamesAreKeyedByTitle() =>
        Assert.Equal("minecraft", AutoProfiles.KeyFor("ApplicationFrameHost", "Minecraft"));

    [Theory]
    [InlineData("Minecraft 1.21.1")]
    [InlineData("Minecraft* 1.21.1 - Singleplayer")]
    [InlineData("Minecraft 1.21.1 - Multiplayer (3rd-party Server)")]
    public void TitleSuffixesAfterADashDontSplitTheGame(string title) =>
        Assert.Equal("minecraft-1-21-1", AutoProfiles.KeyFor("javaw", title));

    [Fact]
    public void TitleKeyedProfilesStoreTheTrimmedTitle()
    {
        var p = AutoProfiles.TryCreate(_data, Template, "javaw", "Minecraft 1.21.1 - Singleplayer", [])!;

        Assert.Equal(["Minecraft 1.21.1"], p.WindowMatch!.Titles);
        Assert.Equal("Minecraft 1.21.1", p.DisplayName);
    }

    [Fact]
    public void JavaWithEmptyTitleGetsNoProfile() => Assert.Null(AutoProfiles.KeyFor("javaw", "  "));

    [Fact]
    public void TryCreateWritesProfileJsonAndReturnsAutoProfile()
    {
        var p = AutoProfiles.TryCreate(_data, Template, "Valheim.exe", "  Valheim  ", [])!;

        Assert.True(p.IsAuto);
        Assert.Equal("valheim", p.Id);
        Assert.Equal("Valheim", p.DisplayName);
        Assert.Equal("Notes", p.TrackingLabel);
        Assert.Equal(Template, p.InstructionsPath);
        Assert.Equal(Path.Combine(_data, "valheim"), p.FolderPath);
        Assert.Equal(["valheim"], p.WindowMatch!.ProcessNames);
        Assert.Empty(p.WindowMatch.Titles);
        Assert.True(File.Exists(Path.Combine(_data, "valheim", "profile.json")));

        var reloaded = ProfileLoader.LoadAll(TestPaths.BuiltInProfilesRoot, _data, []).Profiles.Single(x => x.Id == "valheim");
        Assert.Equal(p.DisplayName, reloaded.DisplayName);
        Assert.Equal(p.WindowMatch.ProcessNames, reloaded.WindowMatch!.ProcessNames);
        Assert.True(reloaded.IsAuto);
    }

    [Fact]
    public void BlankTitleUsesTheProcessName() =>
        Assert.Equal("valheim", AutoProfiles.TryCreate(_data, Template, "valheim", "", [])!.DisplayName);

    [Fact]
    public void JavaProfilesMatchByTitle()
    {
        var p = AutoProfiles.TryCreate(_data, Template, "javaw", "Minecraft 1.21", [])!;

        Assert.Equal("minecraft-1-21", p.Id);
        Assert.Equal(["Minecraft 1.21"], p.WindowMatch!.Titles);
        Assert.Empty(p.WindowMatch.ProcessNames);
    }

    [Fact]
    public void NotGamesAreRefused() => Assert.Null(AutoProfiles.TryCreate(_data, Template, "launcher", "Launcher", ["launcher"]));

    [Fact]
    public void SkippedProcessesAreRefused() => Assert.Null(AutoProfiles.TryCreate(_data, Template, "chrome", "Wiki", []));

    [Fact]
    public void ExistingAutoProfileIsReused()
    {
        AutoProfiles.TryCreate(_data, Template, "valheim", "Valheim", []);
        var json = Path.Combine(_data, "valheim", "profile.json");
        var before = File.ReadAllText(json);

        var again = AutoProfiles.TryCreate(_data, Template, "valheim", "Valheim (new title)", [])!;

        Assert.Equal("Valheim", again.DisplayName);
        Assert.Equal(before, File.ReadAllText(json));
    }

    [Fact]
    public void PersonalClaudeMdIsUsed()
    {
        Directory.CreateDirectory(Path.Combine(_data, "valheim"));
        File.WriteAllText(Path.Combine(_data, "valheim", "CLAUDE.md"), "# mine");

        Assert.Equal(Path.Combine(_data, "valheim", "CLAUDE.md"), AutoProfiles.TryCreate(_data, Template, "valheim", "Valheim", [])!.InstructionsPath);
    }
}
