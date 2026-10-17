using ClaudeGameHelper.Core.Profiles;
using ClaudeGameHelper.Core.Settings;
using ClaudeGameHelper.Core.Watch;

namespace ClaudeGameHelper.Tray;

/// <summary>What the tray's "Watch mode" submenu shows and changes.</summary>
public interface IWatchMenu
{
    /// <summary>The game in front, or null when there isn't one.</summary>
    string? GameName { get; }

    /// <summary>False for "No game" and for games whose profile doesn't allow watching.</summary>
    bool Allowed { get; }

    bool FollowGoal { get; set; }
    bool GeneralTips { get; set; }
    int CheckEverySeconds { get; set; }
    bool Chime { get; set; }
}

/// <summary>Watch switches for the active game plus the global frequency and chime, saved on every change.</summary>
public sealed class WatchMenu(Func<AppSettings> get, Action<AppSettings> save, Func<GameProfile> activeProfile) : IWatchMenu
{
    public string? GameName => activeProfile() is { IsFallback: false } p ? p.DisplayName : null;

    public bool Allowed => activeProfile() is { IsFallback: false, AllowWatchMode: true };

    public bool FollowGoal
    {
        get => Switches.FollowGoal;
        set => SetSwitches(Switches with { FollowGoal = value });
    }

    public bool GeneralTips
    {
        get => Switches.GeneralTips;
        set => SetSwitches(Switches with { GeneralTips = value });
    }

    public int CheckEverySeconds
    {
        get => get().Watch.CheckEverySeconds;
        set
        {
            var snapped = WatchSettings.Frequencies.MinBy(f => Math.Abs(f - value));
            SaveWatch(get().Watch with { CheckEverySeconds = snapped });
        }
    }

    public bool Chime
    {
        get => get().Watch.Chime;
        set => SaveWatch(get().Watch with { Chime = value });
    }

    private WatchSwitches Switches => get().Watch.For(activeProfile().Id);

    private void SetSwitches(WatchSwitches switches)
    {
        var games = new Dictionary<string, WatchSwitches>(get().Watch.Games) { [activeProfile().Id] = switches };
        SaveWatch(get().Watch with { Games = games });
    }

    private void SaveWatch(WatchSettings watch) => save(get() with { Watch = watch });
}
