using ClaudeGameHelper.Core.Claude;
using ClaudeGameHelper.Core.Profiles;
using ClaudeGameHelper.Core.Settings;

namespace ClaudeGameHelper.Core.Watch;

public interface IWatchService
{
    WatchState For(GameProfile profile);

    Task<WatchDisplay?> CheckAsync(GameProfile profile, string screenshotPath, WatchSwitches switches,
        string? activeTrackingName, CancellationToken ct);
}

/// <summary>Runs one watch check: a quick, read-only Claude glance at the latest screenshot.</summary>
public sealed class WatchService(AppSettings settings, Func<ClaudeLocateResult> locate, IClaudeRunner runner, Func<DateTime> now)
    : IWatchService
{
    private static readonly TimeSpan DefaultPause = TimeSpan.FromMinutes(30);
    private readonly Dictionary<string, WatchState> _states = [];
    private string? _claudePath;

    public WatchState For(GameProfile profile)
    {
        if (!_states.TryGetValue(profile.Id, out var state))
        {
            state = new WatchState();
            _states[profile.Id] = state;
        }
        return state;
    }

    public async Task<WatchDisplay?> CheckAsync(GameProfile profile, string screenshotPath, WatchSwitches switches,
        string? activeTrackingName, CancellationToken ct)
    {
        var state = For(profile);
        try
        {
            if (_claudePath == null)
            {
                var located = await Task.Run(locate, ct);
                if (located.Path == null)
                {
                    Log.Info("Watch check skipped: Claude Code wasn't found.");
                    return null;
                }
                _claudePath = located.Path;
            }

            var prompt = WatchPrompt.Build(new WatchCheck(profile.DisplayName, screenshotPath, switches,
                switches.FollowGoal && state.Goal != null, state.RecentExchanges, state.TipsGiven,
                profile.TrackingLabel, activeTrackingName));
            Directory.CreateDirectory(profile.FolderPath);

            UsageLimit? limit = null;
            var result = await runner.RunAsync(
                new ClaudeLaunch(_claudePath, ClaudeArgsBuilder.BuildWatch(settings.FastModel, profile.InstructionsPath), profile.FolderPath, prompt),
                e => { if (e is UsageLimit l) limit = l; },
                ct);

            if (result.Canceled || result.Completion is not { } done)
                return null;

            if (limit != null || (done.IsError && done.ResultText.Contains("limit", StringComparison.OrdinalIgnoreCase)))
            {
                var until = limit?.ResetsAt?.ToLocalTime().DateTime ?? now() + DefaultPause;
                Log.Info($"Watch mode paused until {until:t}: usage limit.");
                return state.Pause(until, $"Watch mode paused: Claude usage limit reached. It resumes at {until:h:mm tt}.");
            }

            return done.IsError ? null : state.Apply(WatchReplyParser.Parse(done.ResultText), now());
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            Log.Error("Watch check failed.", ex);
            _claudePath = null;
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            state.MarkChecked(now());
        }
    }
}
