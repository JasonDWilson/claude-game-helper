using ClaudeGameHelper.Core.Claude;
using ClaudeGameHelper.Core.Conversations;
using ClaudeGameHelper.Core.Profiles;
using ClaudeGameHelper.Core.Settings;

namespace ClaudeGameHelper.Tests.Conversations;

public class AskServiceTests
{
    private delegate Task<ClaudeRunResult> Script(ClaudeLaunch launch, Action<ClaudeEvent> onEvent, CancellationToken ct);

    private sealed class FakeRunner : IClaudeRunner
    {
        public List<ClaudeLaunch> Launches { get; } = [];
        public Queue<Script> Scripts { get; } = new();

        public Task<ClaudeRunResult> RunAsync(ClaudeLaunch launch, Action<ClaudeEvent> onEvent, CancellationToken ct)
        {
            Launches.Add(launch);
            return Scripts.Count > 0 ? Scripts.Dequeue()(launch, onEvent, ct) : Answer("ok")(launch, onEvent, ct);
        }
    }

    private static Script Answer(string text, int contextTokens = 1000, bool isError = false) => (_, onEvent, _) =>
    {
        onEvent(new SessionStarted("ignored", "claude-sonnet-5-5"));
        onEvent(new ContextSize(contextTokens));
        var done = new Completed(text, isError, "success");
        onEvent(done);
        return Task.FromResult(new ClaudeRunResult(0, false, "", done));
    };

    private readonly FakeRunner _runner = new();
    private readonly GameProfile _profile;
    private readonly string _readOnlyDir = TestPaths.NewTempDir();

    public AskServiceTests()
    {
        var dir = TestPaths.NewTempDir();
        File.WriteAllText(Path.Combine(dir, "CLAUDE.md"), "# test");
        _profile = new GameProfile("g", "Game", dir, null, [_readOnlyDir, @"C:\does\not\exist"], "Character", null, null, false, false);
    }

    private AskService Service(AppSettings? settings = null, ClaudeLocateResult? locate = null) =>
        new(settings ?? new AppSettings(), () => locate ?? new ClaudeLocateResult(@"C:\claude.exe", [@"C:\claude.exe"]), _runner,
            () => new DateTime(2026, 10, 16, 20, 15, 0));

    private AskRequest Request(string question = "Is this good?", IReadOnlyList<string>? shots = null, bool deep = false, GameProfile? profile = null) =>
        new(profile ?? _profile, question, shots ?? [], deep, "vault-dweller");

    private static string ArgAfter(ClaudeLaunch launch, string flag) => launch.Args[launch.Args.ToList().IndexOf(flag) + 1];

    [Fact]
    public async Task FirstAskUsesSessionIdThenResumes()
    {
        var service = Service();

        await service.AskAsync(Request(), _ => { }, CancellationToken.None);
        await service.AskAsync(Request("And this?"), _ => { }, CancellationToken.None);

        var first = _runner.Launches[0];
        var second = _runner.Launches[1];
        Assert.Equal(@"C:\claude.exe", first.ExePath);
        Assert.Equal(_profile.FolderPath, first.WorkingDirectory);
        Assert.Equal(PromptBuilder.Build("Is this good?", [], "Game", "Character", "vault-dweller"), first.StdinText);
        Assert.Equal(_profile.InstructionsPath, ArgAfter(first, "--append-system-prompt-file"));
        Assert.Equal(ArgAfter(first, "--session-id"), ArgAfter(second, "--resume"));
        Assert.DoesNotContain("--session-id", second.Args);
    }

    [Fact]
    public async Task OnlyExistingReadOnlyDirsArePassed()
    {
        await Service().AskAsync(Request(), _ => { }, CancellationToken.None);

        var launch = _runner.Launches.Single();
        Assert.Equal(1, launch.Args.Count(a => a == "--add-dir"));
        Assert.Equal(_readOnlyDir, ArgAfter(launch, "--add-dir"));
    }

    [Fact]
    public async Task ModelFollowsScreenshotsAndDeep()
    {
        var service = Service();

        var fast = await service.AskAsync(Request(shots: ["a.jpg"]), _ => { }, CancellationToken.None);
        var deep = await service.AskAsync(Request(shots: ["a.jpg", "b.jpg"]), _ => { }, CancellationToken.None);

        Assert.Equal("sonnet", fast.Model);
        Assert.Equal("low", ArgAfter(_runner.Launches[0], "--effort"));
        Assert.Equal("opus", deep.Model);
        Assert.Equal("opus", ArgAfter(_runner.Launches[1], "--model"));
    }

    [Fact]
    public async Task SuccessReturnsResultTextForwardsEventsAndArchivesTurn()
    {
        _runner.Scripts.Enqueue(Answer("Scrip it."));
        var events = new List<ClaudeEvent>();
        var service = Service();

        var outcome = await service.AskAsync(Request(), events.Add, CancellationToken.None);

        Assert.Equal(new AskOutcome("Scrip it.", false, false, false, "sonnet"), outcome);
        Assert.Contains(events, e => e is Completed);
        var archive = File.ReadAllText(Path.Combine(_profile.ConversationsDir, "2026-10-16_201500.md"));
        Assert.Contains("Is this good?", archive);
        Assert.Contains("Scrip it.", archive);
    }

    [Fact]
    public async Task ClaudeMissingIsAPlainError()
    {
        var outcome = await Service(locate: new ClaudeLocateResult(null, ["a", "b"])).AskAsync(Request(), _ => { }, CancellationToken.None);

        Assert.True(outcome.IsError);
        Assert.Equal("Claude Code wasn't found. Tried: a, b. Set \"claudePath\" in settings.json to a working claude.exe.", outcome.AnswerText);
        Assert.Empty(_runner.Launches);
    }

    [Fact]
    public async Task LocateIsRetriedAfterAFailureAndCachedAfterSuccess()
    {
        var calls = 0;
        var service = new AskService(new AppSettings(), () => ++calls == 1
                ? new ClaudeLocateResult(null, ["a"])
                : new ClaudeLocateResult(@"C:\claude.exe", [@"C:\claude.exe"]),
            _runner, () => DateTime.Now);

        await service.AskAsync(Request(), _ => { }, CancellationToken.None);
        await service.AskAsync(Request(), _ => { }, CancellationToken.None);
        await service.AskAsync(Request(), _ => { }, CancellationToken.None);

        Assert.Equal(2, calls);
        Assert.Equal(2, _runner.Launches.Count);
    }

    [Fact]
    public async Task UsageLimitResultIsShownAsError()
    {
        _runner.Scripts.Enqueue(Answer("You've hit your limit · resets 3pm", isError: true));

        var outcome = await Service().AskAsync(Request(), _ => { }, CancellationToken.None);

        Assert.True(outcome.IsError);
        Assert.Equal("You've hit your limit · resets 3pm", outcome.AnswerText);
    }

    [Fact]
    public async Task ErrorResultWithoutTextSaysSo()
    {
        _runner.Scripts.Enqueue(Answer("", isError: true));

        var outcome = await Service().AskAsync(Request(), _ => { }, CancellationToken.None);

        Assert.Equal("Claude couldn't answer (no details).", outcome.AnswerText);
    }

    [Fact]
    public async Task CrashWithoutResultReportsExitAndStderr()
    {
        _runner.Scripts.Enqueue((_, _, _) => Task.FromResult(new ClaudeRunResult(1, false, "boom\nsecond line\n", null)));

        var outcome = await Service().AskAsync(Request(), _ => { }, CancellationToken.None);

        Assert.True(outcome.IsError);
        Assert.Equal("Claude Code stopped unexpectedly (exit 1): boom", outcome.AnswerText);
    }

    [Fact]
    public async Task FailureToStartIsAPlainErrorAndLocatesAgainNextTime()
    {
        var calls = 0;
        var service = new AskService(new AppSettings(), () => { calls++; return new ClaudeLocateResult(@"C:\gone\claude.exe", []); },
            _runner, () => DateTime.Now);
        _runner.Scripts.Enqueue((_, _, _) => throw new System.ComponentModel.Win32Exception("The system cannot find the file specified."));

        var outcome = await service.AskAsync(Request(), _ => { }, CancellationToken.None);
        await service.AskAsync(Request(), _ => { }, CancellationToken.None);

        Assert.True(outcome.IsError);
        Assert.Equal(@"Couldn't start Claude Code (C:\gone\claude.exe): The system cannot find the file specified.", outcome.AnswerText);
        Assert.Equal(2, calls);
        Assert.False(service.IsBusy);
    }

    [Fact]
    public async Task SecondAskWhileBusyIsRefused()
    {
        var release = new TaskCompletionSource();
        _runner.Scripts.Enqueue(async (l, e, c) => { await release.Task; return await Answer("first")(l, e, c); });
        var service = Service();

        var first = service.AskAsync(Request(), _ => { }, CancellationToken.None);
        Assert.True(service.IsBusy);
        var second = await service.AskAsync(Request(), _ => { }, CancellationToken.None);
        release.SetResult();
        await first;

        Assert.True(second.IsError);
        Assert.Equal("Still answering — wait for it or press Stop.", second.AnswerText);
        Assert.Single(_runner.Launches);
        Assert.False(service.IsBusy);
    }

    [Fact]
    public async Task CancelReturnsStopped()
    {
        _runner.Scripts.Enqueue((_, _, _) => Task.FromResult(new ClaudeRunResult(null, true, "", null)));

        var outcome = await Service().AskAsync(Request(), _ => { }, CancellationToken.None);

        Assert.Equal("Stopped.", outcome.AnswerText);
        Assert.True(outcome.Canceled);
        Assert.False(outcome.IsError);
    }

    [Fact]
    public async Task RolloverIsReported()
    {
        _runner.Scripts.Enqueue(Answer("first", contextTokens: 10));
        var service = Service(new AppSettings { RolloverContextTokens = 10 });

        var first = await service.AskAsync(Request(), _ => { }, CancellationToken.None);
        var second = await service.AskAsync(Request(), _ => { }, CancellationToken.None);

        Assert.False(first.RolledOver);
        Assert.True(second.RolledOver);
        Assert.Contains("--session-id", _runner.Launches[1].Args);
        Assert.NotEqual(ArgAfter(_runner.Launches[0], "--session-id"), ArgAfter(_runner.Launches[1], "--session-id"));
    }

    [Fact]
    public async Task SessionsAreSeparatePerProfile()
    {
        var other = _profile with { Id = "other", FolderPath = TestPaths.NewTempDir() };
        var service = Service();

        await service.AskAsync(Request(), _ => { }, CancellationToken.None);
        await service.AskAsync(Request(profile: other), _ => { }, CancellationToken.None);

        Assert.Contains("--session-id", _runner.Launches[1].Args);
        Assert.NotEqual(ArgAfter(_runner.Launches[0], "--session-id"), ArgAfter(_runner.Launches[1], "--session-id"));
    }

    [Fact]
    public async Task NewConversationDuringAnAnswerLeavesTheNewSessionUnstarted()
    {
        var service = Service();
        _runner.Scripts.Enqueue((l, e, c) =>
        {
            service.NewConversation(_profile);
            return Answer("old conversation", contextTokens: 500_000)(l, e, c);
        });

        await service.AskAsync(Request(), _ => { }, CancellationToken.None);
        var next = await service.AskAsync(Request(), _ => { }, CancellationToken.None);

        Assert.Contains("--session-id", _runner.Launches[1].Args);
        Assert.NotEqual(ArgAfter(_runner.Launches[0], "--session-id"), ArgAfter(_runner.Launches[1], "--session-id"));
        Assert.False(next.RolledOver);
    }

    [Fact]
    public async Task MissingDataFolderIsCreatedBeforeClaudeRuns()
    {
        var profile = _profile with { FolderPath = Path.Combine(TestPaths.NewTempDir(), "not-yet"), InstructionsPath = _profile.InstructionsPath };

        await Service().AskAsync(Request(profile: profile), _ => { }, CancellationToken.None);

        Assert.True(Directory.Exists(profile.FolderPath));
        Assert.Equal(profile.FolderPath, _runner.Launches.Single().WorkingDirectory);
    }

    [Fact]
    public async Task GameNameGoesInThePromptExceptForTheFallback()
    {
        var fallback = _profile with { Id = "general", DisplayName = "No game", IsFallback = true };
        var service = Service();

        await service.AskAsync(Request(), _ => { }, CancellationToken.None);
        await service.AskAsync(Request(profile: fallback), _ => { }, CancellationToken.None);

        Assert.Contains("Game: Game", _runner.Launches[0].StdinText);
        Assert.DoesNotContain("Game:", _runner.Launches[1].StdinText);
    }

    [Fact]
    public async Task NewConversationStartsFresh()
    {
        var service = Service();
        await service.AskAsync(Request(), _ => { }, CancellationToken.None);

        service.NewConversation(_profile);
        await service.AskAsync(Request(), _ => { }, CancellationToken.None);

        Assert.Contains("--session-id", _runner.Launches[1].Args);
        Assert.NotEqual(ArgAfter(_runner.Launches[0], "--session-id"), ArgAfter(_runner.Launches[1], "--session-id"));
    }
}
