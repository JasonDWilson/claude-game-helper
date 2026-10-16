using ClaudeGameHelper.Core.Profiles;
using static ClaudeGameHelper.Tests.Profiles.ProfileTestData;

namespace ClaudeGameHelper.Tests.Profiles;

public class ProfileLoaderTests
{
    private static readonly string BuiltIn = TestPaths.BuiltInProfilesRoot;
    private readonly string _data = TestPaths.NewTempDir();

    private void WriteAutoProfile(string id, string displayName)
    {
        var dir = Path.Combine(_data, id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "profile.json"),
            $$"""{ "displayName": "{{displayName}}", "auto": true, "windowMatch": { "processNames": ["{{id}}"] }, "trackingLabel": "Notes" }""");
    }

    [Fact]
    public void BuiltInProfileUsesDataFolderAndBuiltInInstructions()
    {
        var r = ProfileLoader.LoadAll(BuiltIn, _data, []);

        Assert.Empty(r.Errors);
        var f = r.Profiles.Single(p => p.Id == "fallout-76");
        Assert.Equal("Fallout 76", f.DisplayName);
        Assert.Equal("Character", f.TrackingLabel);
        Assert.Contains("Project76_GamePass", f.WindowMatch!.ProcessNames);
        Assert.False(f.IsAuto);
        Assert.Equal(Path.Combine(_data, "fallout-76"), f.FolderPath);
        Assert.Equal(Path.Combine(_data, "fallout-76", "tracking"), f.TrackingDir);
        Assert.Equal(Path.Combine(BuiltIn, "fallout-76", "CLAUDE.md"), f.InstructionsPath);
        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Fallout 76"),
            f.ReadOnlyPaths[0]);
        Assert.Equal(Path.Combine(BuiltIn, "_template", "CLAUDE.md"), r.TemplateInstructionsPath);
        Assert.Equal("general", r.Fallback!.Id);
        Assert.Equal(Path.Combine(_data, "general"), r.Fallback.FolderPath);
        Assert.DoesNotContain(r.Profiles, p => p.Id == "_template");
        Assert.DoesNotContain(r.Profiles, p => p.IsFallback);
    }

    [Fact]
    public void PersonalClaudeMdOverridesBuiltIn()
    {
        Directory.CreateDirectory(Path.Combine(_data, "fallout-76"));
        File.WriteAllText(Path.Combine(_data, "fallout-76", "CLAUDE.md"), "# mine");

        var f = ProfileLoader.LoadAll(BuiltIn, _data, []).Profiles.Single(p => p.Id == "fallout-76");

        Assert.Equal(Path.Combine(_data, "fallout-76", "CLAUDE.md"), f.InstructionsPath);
    }

    [Fact]
    public void AutoProfilesLoadFromDataFolderWithTemplate()
    {
        WriteAutoProfile("valheim", "Valheim");

        var r = ProfileLoader.LoadAll(BuiltIn, _data, []);

        Assert.Empty(r.Errors);
        var v = r.Profiles.Single(p => p.Id == "valheim");
        Assert.True(v.IsAuto);
        Assert.Equal("Valheim", v.DisplayName);
        Assert.Equal("Notes", v.TrackingLabel);
        Assert.Equal(["valheim"], v.WindowMatch!.ProcessNames);
        Assert.Equal(Path.Combine(_data, "valheim"), v.FolderPath);
        Assert.Equal(r.TemplateInstructionsPath, v.InstructionsPath);
    }

    [Fact]
    public void AutoProfileCanHaveAPersonalClaudeMd()
    {
        WriteAutoProfile("valheim", "Valheim");
        File.WriteAllText(Path.Combine(_data, "valheim", "CLAUDE.md"), "# mine");

        var v = ProfileLoader.LoadAll(BuiltIn, _data, []).Profiles.Single(p => p.Id == "valheim");

        Assert.Equal(Path.Combine(_data, "valheim", "CLAUDE.md"), v.InstructionsPath);
    }

    [Fact]
    public void NotGamesAreLeftOut()
    {
        WriteAutoProfile("valheim", "Valheim");

        Assert.DoesNotContain(ProfileLoader.LoadAll(BuiltIn, _data, ["valheim"]).Profiles, p => p.Id == "valheim");
    }

    [Fact]
    public void DataFolderWithoutProfileJsonIsIgnored()
    {
        Directory.CreateDirectory(Path.Combine(_data, "fallout-76", "tracking"));
        Directory.CreateDirectory(Path.Combine(_data, "logs"));

        var r = ProfileLoader.LoadAll(BuiltIn, _data, []);

        Assert.Empty(r.Errors);
        Assert.Single(r.Profiles, p => p.Id == "fallout-76");
        Assert.DoesNotContain(r.Profiles, p => p.Id == "logs");
    }

    [Fact]
    public void MissingDataRootIsFine()
    {
        var r = ProfileLoader.LoadAll(BuiltIn, Path.Combine(_data, "nope"), []);

        Assert.Empty(r.Errors);
        Assert.Single(r.Profiles, p => p.Id == "fallout-76");
    }

    [Fact]
    public void BadJsonIsReportedAndOtherProfilesStillLoad()
    {
        var root = TestPaths.NewTempDir();
        WriteProfile(root, "general", """{ "displayName": "No game", "isFallback": true }""");
        WriteProfile(root, "good", """{ "displayName": "Good", "windowMatch": { "processNames": ["good"] } }""");
        WriteProfile(root, "broken", "{ nope");

        var r = ProfileLoader.LoadAll(root, _data, []);

        Assert.Contains("broken", Assert.Single(r.Errors));
        Assert.Single(r.Profiles, p => p.Id == "good");
    }

    [Fact]
    public void BadAutoProfileJsonIsReported()
    {
        Directory.CreateDirectory(Path.Combine(_data, "oops"));
        File.WriteAllText(Path.Combine(_data, "oops", "profile.json"), "{ nope");

        Assert.Contains(ProfileLoader.LoadAll(BuiltIn, _data, []).Errors, e => e.Contains("oops"));
    }

    [Fact]
    public void MissingClaudeMdIsAnError()
    {
        var root = TestPaths.NewTempDir();
        WriteProfile(root, "general", """{ "isFallback": true }""");
        WriteProfile(root, "nomd", """{ "displayName": "No MD" }""", withClaudeMd: false);

        var r = ProfileLoader.LoadAll(root, _data, []);

        Assert.Contains(r.Errors, e => e.Contains("CLAUDE.md") && e.Contains("nomd"));
        Assert.DoesNotContain(r.Profiles, p => p.Id == "nomd");
    }

    [Fact]
    public void NoFallbackIsAnError()
    {
        var root = TestPaths.NewTempDir();
        WriteProfile(root, "good", """{ "displayName": "Good" }""");

        var r = ProfileLoader.LoadAll(root, _data, []);

        Assert.Null(r.Fallback);
        Assert.Contains(r.Errors, e => e.Contains("fallback"));
    }
}
