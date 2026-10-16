using ClaudeGameHelper.Core.Profiles;
using static ClaudeGameHelper.Tests.Profiles.ProfileTestData;

namespace ClaudeGameHelper.Tests.Profiles;

public class ProfileMatcherTests
{
    private static readonly IReadOnlyList<GameProfile> All =
    [
        Profile("fallout-76", ["Project76_GamePass", "Fallout76"], ["Fallout76", "Fallout 76"]),
        Profile("other", ["othergame"]),
    ];

    [Fact]
    public void MatchesProcessNameIgnoringCaseAndExe() =>
        Assert.Equal("fallout-76", ProfileMatcher.Match(All, "project76_gamepass.exe", "x")!.Id);

    [Fact]
    public void FallsBackToTitle() =>
        Assert.Equal("fallout-76", ProfileMatcher.Match(All, "unknown", "Fallout76")!.Id);

    [Fact]
    public void TitleMatchIgnoresCase() =>
        Assert.Equal("fallout-76", ProfileMatcher.Match(All, "unknown", "fallout 76")!.Id);

    [Fact]
    public void ProcessMatchBeatsTitleMatchOfAnotherProfile()
    {
        IReadOnlyList<GameProfile> profiles = [Profile("a", titles: ["Steam"]), Profile("b", processNames: ["game"])];
        Assert.Equal("b", ProfileMatcher.Match(profiles, "game", "Steam")!.Id);
    }

    [Fact]
    public void BrowserTabAboutTheGameIsNotTheGame() =>
        Assert.Null(ProfileMatcher.Match(All, "chrome", "Fallout 76 wiki - Chrome"));

    [Fact]
    public void ProfileWithoutWindowMatchNeverMatches() =>
        Assert.Null(ProfileMatcher.Match([Profile("none")], "none", "none"));
}
