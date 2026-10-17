namespace ClaudeGameHelper.Core.Watch;

/// <summary>Watch mode's two switches for one game.</summary>
public sealed record WatchSwitches(bool FollowGoal, bool GeneralTips);

/// <summary>Watch mode settings: per-game switches plus the global check frequency and chime.</summary>
public sealed record WatchSettings
{
    public static readonly int[] Frequencies = [20, 30, 60, 120];

    public int CheckEverySeconds { get; init; } = 30;
    public bool Chime { get; init; } = true;
    public IReadOnlyDictionary<string, WatchSwitches> Games { get; init; } = new Dictionary<string, WatchSwitches>();

    /// <summary>The switches for a game; both off for a game never configured.</summary>
    public WatchSwitches For(string profileId) =>
        Games.TryGetValue(profileId, out var switches) ? switches : new WatchSwitches(false, false);
}
