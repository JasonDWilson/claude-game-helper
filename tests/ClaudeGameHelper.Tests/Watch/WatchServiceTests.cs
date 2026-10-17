using ClaudeGameHelper.Core.Claude;
using ClaudeGameHelper.Core.Profiles;
using ClaudeGameHelper.Core.Settings;
using ClaudeGameHelper.Core.Watch;

namespace ClaudeGameHelper.Tests.Watch;

public class WatchServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 20, 0, 0);

    private sealed class FakeRunner : IClaudeRunner
    {
        public List<ClaudeLaunch> Launches { get; } = [];
        public Func<Action<ClaudeEvent>, ClaudeRunResult> Script { get; set; } = _ => Result("""{"verdict":"nothing","message":""}""");

        public Task<ClaudeRunResult> RunAsync(ClaudeLaunch launch, Action<ClaudeEvent> onEvent, CancellationToken ct)
        {
            Launches.Add(launch);
            return Task.FromResult(Script(onEvent));
        }
    }

    private static ClaudeRunResult Result(string text, bool isError = false) =>
        new(0, false, "", new Completed(text, isError, "success"));

    private readonly FakeRunner _runner = new();
    private readonly GameProfile _profile;
    private readonly string _shot;

    public WatchServiceTests()
    {
        var dir = Path.Combine(TestPaths.NewTempDir(), "game");
        _profile = new GameProfile("game", "Game", dir, null, [], "Character", null, null, true, false)
        {
            InstructionsPath = Path.Combine(TestPaths.BuiltInProfilesRoot, "_template", "CLAUDE.md"),
        };
        _shot = Path.Combine(dir, "screenshots", "watch.jpg");
    }

    private WatchService Service(ClaudeLocateResult? locate = null) =>
        new(new AppSettings(), () => locate ?? new ClaudeLocateResult(@"C:\claude.exe", []), _runner, () => Now);

    private static readonly WatchSwitches GoalOnly = new(true, false);

    [Fact]
    public async Task CheckRunsWatchArgsWithPromptOnStdinAndAppliesReply()
    {
        _runner.Script = _ => Result("""{"verdict":"goal_alert","message":"Wrong way."}""");
        var service = Service();
        var state = service.For(_profile);
        state.RecordExchange("Where's the Whitespring?", "Northeast.", false, true, Now);

        var display = await service.CheckAsync(_profile, _shot, GoalOnly, "vault-dweller", CancellationToken.None);

        Assert.Equal(new WatchDisplay(WatchMessageKind.GoalAlert, "Wrong way."), display);
        var launch = _runner.Launches.Single();
        Assert.Equal(@"C:\claude.exe", launch.ExePath);
        Assert.Equal(ClaudeArgsBuilder.BuildWatch("sonnet", _profile.InstructionsPath), launch.Args);
        Assert.Equal(_profile.FolderPath, launch.WorkingDirectory);
        Assert.True(Directory.Exists(_profile.FolderPath));
        Assert.Equal(WatchPrompt.Build(new WatchCheck("Game", _shot, GoalOnly, true, state.RecentExchanges, [], "Character", "vault-dweller")),
            launch.StdinText);
        Assert.Equal(WatchDecision.NotDue,
            state.Decide(new WatchInputs(Now.AddSeconds(5), GoalOnly, true, true, false, false, 30)));
    }

    [Fact]
    public void StateIsPerProfile()
    {
        var service = Service();

        Assert.Same(service.For(_profile), service.For(_profile));
        Assert.NotSame(service.For(_profile), service.For(_profile with { Id = "other" }));
    }

    [Fact]
    public async Task UsageLimitPausesWithOneNote()
    {
        var resets = new DateTimeOffset(2026, 10, 16, 23, 0, 0, TimeSpan.Zero);
        _runner.Script = onEvent =>
        {
            onEvent(new UsageLimit(resets));
            return Result("You've hit your limit", isError: true);
        };
        var service = Service();

        var display = await service.CheckAsync(_profile, _shot, GoalOnly, null, CancellationToken.None);

        Assert.Equal(new WatchDisplay(WatchMessageKind.Paused,
            $"Watch mode paused: Claude usage limit reached. It resumes at {resets.ToLocalTime():h:mm tt}."), display);
        Assert.Equal(resets.ToLocalTime().DateTime, service.For(_profile).PausedUntil);
    }

    [Fact]
    public async Task UsageLimitWithoutResetTimePausesThirtyMinutes()
    {
        _runner.Script = _ => Result("Claude AI usage limit reached", isError: true);
        var service = Service();

        var display = await service.CheckAsync(_profile, _shot, GoalOnly, null, CancellationToken.None);

        Assert.Equal(WatchMessageKind.Paused, display!.Kind);
        Assert.Equal(Now.AddMinutes(30), service.For(_profile).PausedUntil);
    }

    [Fact]
    public async Task OtherErrorsShowNothing()
    {
        _runner.Script = _ => Result("something broke", isError: true);

        Assert.Null(await Service().CheckAsync(_profile, _shot, GoalOnly, null, CancellationToken.None));
    }

    [Fact]
    public async Task CrashOrCancelShowsNothing()
    {
        _runner.Script = _ => new ClaudeRunResult(1, false, "boom", null);
        Assert.Null(await Service().CheckAsync(_profile, _shot, GoalOnly, null, CancellationToken.None));

        _runner.Script = _ => new ClaudeRunResult(null, true, "", null);
        Assert.Null(await Service().CheckAsync(_profile, _shot, GoalOnly, null, CancellationToken.None));
    }

    [Fact]
    public async Task RunnerExceptionShowsNothing()
    {
        _runner.Script = _ => throw new System.ComponentModel.Win32Exception("gone");

        Assert.Null(await Service().CheckAsync(_profile, _shot, GoalOnly, null, CancellationToken.None));
    }

    [Fact]
    public async Task ClaudeMissingShowsNothing()
    {
        Assert.Null(await Service(new ClaudeLocateResult(null, ["a"])).CheckAsync(_profile, _shot, GoalOnly, null, CancellationToken.None));
        Assert.Empty(_runner.Launches);
    }
}
