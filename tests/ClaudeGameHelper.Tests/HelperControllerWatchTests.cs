using ClaudeGameHelper.Core.Claude;
using ClaudeGameHelper.Core.Conversations;
using ClaudeGameHelper.Core.Profiles;
using ClaudeGameHelper.Core.Watch;
using ClaudeGameHelper.Panel;
using ClaudeGameHelper.Platform;

namespace ClaudeGameHelper.Tests;

public class HelperControllerWatchTests
{
    private const int OwnPid = 4242;
    private static readonly IntPtr GameHwnd = new(1001);

    private sealed class Ask : IAskService
    {
        public bool IsBusy { get; set; }
        public List<AskRequest> Requests { get; } = [];
        public List<string> Notes { get; } = [];
        public Func<CancellationToken, Task>? BeforeAnswer { get; set; }

        public async Task<AskOutcome> AskAsync(AskRequest request, Action<ClaudeEvent> onEvent, CancellationToken ct)
        {
            Requests.Add(request);
            if (BeforeAnswer != null)
                await BeforeAnswer(ct);
            return new AskOutcome("Head northeast.", false, false, false, "sonnet");
        }

        public void NewConversation(GameProfile profile) { }
        public void ArchiveNote(GameProfile profile, string note) => Notes.Add(note);
    }

    private sealed class Capture : IGameCapture
    {
        public byte[] Thumbnail { get; set; } = new byte[FrameDiff.Width * FrameDiff.Height];
        public string? FailWith { get; set; }
        public List<(IntPtr Hwnd, string Path)> WatchCaptures { get; } = [];

        public Task<string> CaptureAsync(IntPtr hwnd, string screenshotsDir) => Task.FromResult("shot.jpg");

        public Task<WatchFrame> CaptureForWatchAsync(IntPtr hwnd, string jpgPath)
        {
            if (FailWith != null)
                throw new CaptureFailedException(FailWith);
            WatchCaptures.Add((hwnd, jpgPath));
            return Task.FromResult(new WatchFrame(jpgPath, Thumbnail));
        }
    }

    private sealed class Panel : IPanelView
    {
        public bool IsShown { get; set; }
        public bool IsActive { get; set; }
        public int ShowPassiveCalls { get; private set; }
        public void ShowPassive() { ShowPassiveCalls++; IsShown = true; }
        public void ShowForAsk() { IsShown = true; IsActive = true; }
        public void HidePanel() { IsShown = false; IsActive = false; }
    }

    private sealed class Watch : IWatchService
    {
        private readonly Dictionary<string, WatchState> _states = [];
        public Queue<WatchDisplay?> Displays { get; } = new();
        public TaskCompletionSource? Gate { get; set; }
        public int Checks { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public Func<DateTime> Now { get; set; } = () => DateTime.Now;

        public WatchState For(GameProfile profile)
        {
            if (!_states.TryGetValue(profile.Id, out var s))
                _states[profile.Id] = s = new WatchState();
            return s;
        }

        public async Task<WatchDisplay?> CheckAsync(GameProfile profile, string screenshotPath, WatchSwitches switches,
            string? activeTrackingName, CancellationToken ct)
        {
            Checks++;
            LastToken = ct;
            if (Gate != null)
                await Gate.Task;
            For(profile).MarkChecked(Now());
            var display = Displays.Count > 0 ? Displays.Dequeue() : null;
            if (display?.Kind == WatchMessageKind.GoalReached)
                For(profile).StopGoal();   // as the real service's WatchState.Apply does
            return display;
        }
    }

    private readonly Ask _ask = new();
    private readonly Capture _capture = new();
    private readonly Panel _panel = new();
    private readonly PanelViewModel _vm = new();
    private readonly Watch _watch = new();
    private readonly GameProfile _game;
    private readonly GameProfile _fallback;
    private WatchSettings _settings;
    private DateTime _now = new(2026, 10, 17, 20, 0, 0);
    private int _chimes;
    private readonly List<bool> _attention = [];
    private readonly HelperController _controller;

    public HelperControllerWatchTests()
    {
        _game = new GameProfile("game", "Game", TestPaths.NewTempDir(), new WindowMatch(["thegame"], []), [], "Character", null, null, true, false);
        _fallback = new GameProfile("general", "No game", TestPaths.NewTempDir(), null, [], null, null, null, false, true);
        _settings = With(new WatchSwitches(true, false));
        _watch.Now = () => _now;
        _controller = new HelperController([_game], _fallback, _ask, _capture, _panel, _vm, _ => { }, OwnPid, _ => null, _ => { },
            new WatchDependencies(_watch, () => _settings, () => _chimes++, _attention.Add, () => _now));
        GameInFront();
    }

    private WatchSettings With(WatchSwitches switches, string? id = null) =>
        new() { Games = new Dictionary<string, WatchSwitches> { [id ?? "game"] = switches } };

    private void GameInFront() => _controller.OnForegroundChanged(new WindowInfo(GameHwnd, 77, "thegame", "The Game"));

    private async Task AskTyped(string question = "Where's the Whitespring?")
    {
        await _controller.OnAskHotkeyAsync();
        _vm.QuestionText = question;
        await _controller.SendAskBoxAsync();
        _panel.HidePanel();
    }

    [Fact]
    public async Task AnsweredTypedQuestionStartsTheGoalAndShowsIt()
    {
        await AskTyped();

        Assert.Equal("Where's the Whitespring?", _vm.WatchGoalText);
        Assert.Equal("Where's the Whitespring?", _watch.For(_game).Goal!.Label);
    }

    [Fact]
    public async Task ScreenHotkeyAnswerDoesNotStartAGoal()
    {
        await _controller.OnScreenHotkeyAsync();

        Assert.Null(_watch.For(_game).Goal);
        Assert.Null(_vm.WatchGoalText);
    }

    [Fact]
    public async Task TickChecksAndShowsAGoalAlertWithChimeAndPanel()
    {
        await AskTyped();
        _watch.Displays.Enqueue(new WatchDisplay(WatchMessageKind.GoalAlert, "Wrong way."));

        await _controller.OnWatchTickAsync();

        Assert.Equal((GameHwnd, Path.Combine(_game.ScreenshotsDir, "watch.jpg")), Assert.Single(_capture.WatchCaptures));
        var message = _vm.Messages[^1];
        Assert.Equal(ChatRole.Watch, message.Role);
        Assert.True(message.IsAlert);
        Assert.Equal("Wrong way.", message.Text);
        Assert.Equal(1, _panel.ShowPassiveCalls - 1);   // one from the earlier answer, one for the alert
        Assert.Equal(1, _chimes);
        Assert.Contains("👀 Watch: Wrong way.", _ask.Notes);
    }

    [Fact]
    public async Task TipChimesAndSetsAttentionButNeverOpensThePanel()
    {
        _settings = With(new WatchSwitches(false, true));
        _watch.Displays.Enqueue(new WatchDisplay(WatchMessageKind.Tip, "Legendary enemy ahead."));
        var before = _panel.ShowPassiveCalls;

        await _controller.OnWatchTickAsync();

        Assert.Equal(before, _panel.ShowPassiveCalls);
        Assert.Equal(1, _chimes);
        Assert.Equal([true], _attention);
        Assert.False(_vm.Messages[^1].IsAlert);
    }

    [Fact]
    public async Task UnchangedFrameSkipsClaude()
    {
        await AskTyped();
        await _controller.OnWatchTickAsync();
        _now = _now.AddSeconds(31);

        await _controller.OnWatchTickAsync();

        Assert.Equal(2, _capture.WatchCaptures.Count);
        Assert.Equal(1, _watch.Checks);
    }

    [Fact]
    public async Task NoChecksWhileAnotherAppIsInFront()
    {
        await AskTyped();
        _controller.OnForegroundChanged(new WindowInfo(new IntPtr(6), 88, "chrome", "Wiki - Chrome"));

        await _controller.OnWatchTickAsync();

        Assert.Empty(_capture.WatchCaptures);
        Assert.Equal(0, _watch.Checks);
    }

    [Fact]
    public async Task AskingCancelsARunningCheckAndDropsItsResult()
    {
        await AskTyped();
        _watch.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _watch.Displays.Enqueue(new WatchDisplay(WatchMessageKind.GoalAlert, "Stale alert."));
        var tick = _controller.OnWatchTickAsync();

        await AskTyped("Which road?");
        Assert.True(_watch.LastToken.IsCancellationRequested);
        _watch.Gate.SetResult();
        await tick;

        Assert.DoesNotContain(_vm.Messages, m => m.Text == "Stale alert.");
    }

    [Fact]
    public async Task CaptureFailureOnATickIsSilent()
    {
        await AskTyped();
        _vm.StatusText = null;
        _capture.FailWith = "The game window is minimized.";

        await _controller.OnWatchTickAsync();

        Assert.Null(_vm.StatusText);
        Assert.Equal(0, _watch.Checks);
    }

    [Fact]
    public async Task StopWatchingClearsGoalAndHeader()
    {
        await AskTyped();

        _controller.StopWatching();

        Assert.Null(_watch.For(_game).Goal);
        Assert.Null(_vm.WatchGoalText);
    }

    [Fact]
    public async Task GoalReachedClearsHeader()
    {
        await AskTyped();
        _watch.Displays.Enqueue(new WatchDisplay(WatchMessageKind.GoalReached, "You're there."));

        await _controller.OnWatchTickAsync();

        Assert.Null(_vm.WatchGoalText);
        Assert.Equal("You're there.", _vm.Messages[^1].Text);
    }

    [Fact]
    public async Task RecentWatchMessagesRideAlongWithTheNextQuestion()
    {
        await AskTyped();
        _watch.For(_game).Apply(new WatchReply(WatchVerdict.Tip, "Plan on the vendor."), _now);

        await AskTyped("Which vendor?");

        Assert.Equal(["Plan on the vendor."], _ask.Requests[^1].RecentWatch);
    }

    [Fact]
    public async Task FallbackNeverWatches()
    {
        _controller.OnForegroundChanged(new WindowInfo(new IntPtr(7), 89, "notepad", "Untitled"));
        _settings = With(new WatchSwitches(true, true), "general");

        await _controller.OnWatchTickAsync();

        Assert.Equal(0, _watch.Checks);
    }

    [Fact]
    public async Task TicksDontOverlap()
    {
        await AskTyped();
        _watch.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = _controller.OnWatchTickAsync();

        await _controller.OnWatchTickAsync();
        _watch.Gate.SetResult();
        await first;

        Assert.Equal(1, _watch.Checks);
    }

    [Fact]
    public async Task WithoutWatchDependenciesTicksDoNothing()
    {
        var plain = new HelperController([_game], _fallback, _ask, _capture, _panel, new PanelViewModel(), _ => { }, OwnPid, _ => null, _ => { });

        await plain.OnWatchTickAsync();

        Assert.Empty(_capture.WatchCaptures);
    }
}
