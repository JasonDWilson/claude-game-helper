using ClaudeGameHelper.Core.Profiles;
using ClaudeGameHelper.Core.Settings;
using ClaudeGameHelper.Core.Watch;
using ClaudeGameHelper.Tray;

namespace ClaudeGameHelper.Tests.Tray;

public class WatchMenuTests
{
    private AppSettings _settings = new();
    private GameProfile _active;
    private readonly WatchMenu _menu;
    private int _saves;

    private static GameProfile Game(string id, bool allowWatch = true, bool fallback = false) =>
        new(id, id.ToUpperInvariant(), @"C:\" + id, null, [], null, null, null, allowWatch, fallback);

    public WatchMenuTests()
    {
        _active = Game("fallout-76");
        _menu = new WatchMenu(() => _settings, s => { _settings = s; _saves++; }, () => _active);
    }

    [Fact]
    public void SwitchesArePerGame()
    {
        _menu.FollowGoal = true;

        Assert.Equal(new WatchSwitches(true, false), _settings.Watch.For("fallout-76"));
        Assert.True(_menu.FollowGoal);
        Assert.Equal(1, _saves);

        _active = Game("valheim");
        Assert.False(_menu.FollowGoal);

        _menu.GeneralTips = true;
        Assert.Equal(new WatchSwitches(false, true), _settings.Watch.For("valheim"));
        Assert.Equal(new WatchSwitches(true, false), _settings.Watch.For("fallout-76"));
    }

    [Fact]
    public void FrequencyAndChimeAreGlobalAndSaved()
    {
        _menu.CheckEverySeconds = 60;
        _menu.Chime = false;

        Assert.Equal(60, _settings.Watch.CheckEverySeconds);
        Assert.False(_settings.Watch.Chime);
        _active = Game("valheim");
        Assert.Equal(60, _menu.CheckEverySeconds);
        Assert.False(_menu.Chime);
    }

    [Theory]
    [InlineData(45, 30)]
    [InlineData(500, 120)]
    [InlineData(1, 20)]
    [InlineData(90, 60)]
    public void FrequencyOutsideTheChoicesSnapsToNearest(int asked, int expected)
    {
        _menu.CheckEverySeconds = asked;

        Assert.Equal(expected, _settings.Watch.CheckEverySeconds);
    }

    [Fact]
    public void GameNameAndAllowed()
    {
        Assert.Equal("FALLOUT-76", _menu.GameName);
        Assert.True(_menu.Allowed);

        _active = Game("general", fallback: true);
        Assert.Null(_menu.GameName);
        Assert.False(_menu.Allowed);

        _active = Game("shooter", allowWatch: false);
        Assert.Equal("SHOOTER", _menu.GameName);
        Assert.False(_menu.Allowed);
    }

    [Fact]
    public void OtherSettingsAreKept()
    {
        _settings = new AppSettings { FastModel = "haiku", NotGames = ["launcher"] };

        _menu.FollowGoal = true;

        Assert.Equal("haiku", _settings.FastModel);
        Assert.Equal(["launcher"], _settings.NotGames);
    }
}
