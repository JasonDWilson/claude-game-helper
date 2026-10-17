using ClaudeGameHelper.Core.Claude;
using ClaudeGameHelper.Core.Conversations;
using ClaudeGameHelper.Core.Profiles;
using ClaudeGameHelper.Panel;
using ClaudeGameHelper.Platform;

namespace ClaudeGameHelper.Tests;

public class HelperControllerTests
{
    private const int OwnPid = 4242;
    private static readonly IntPtr GameHwnd = new(1001);

    private sealed class FakeAsk : IAskService
    {
        public bool IsBusy { get; set; }
        public List<AskRequest> Requests { get; } = [];
        public List<GameProfile> NewConversations { get; } = [];
        public Action<AskRequest>? OnAsk { get; set; }

        public Task<AskOutcome> AskAsync(AskRequest request, Action<ClaudeEvent> onEvent, CancellationToken ct)
        {
            Requests.Add(request);
            OnAsk?.Invoke(request);
            onEvent(new TextDelta("partial"));
            return Task.FromResult(new AskOutcome("answer", false, false, false, "sonnet"));
        }

        public void NewConversation(GameProfile profile) => NewConversations.Add(profile);
        public void ArchiveNote(GameProfile profile, string note) { }
    }

    private sealed class FakeCapture : IGameCapture
    {
        private int _count;
        public string? FailWith { get; set; }
        public List<IntPtr> Captured { get; } = [];

        public Task<string> CaptureAsync(IntPtr hwnd, string screenshotsDir)
        {
            if (FailWith != null)
                throw new CaptureFailedException(FailWith);
            Captured.Add(hwnd);
            return Task.FromResult($"shot{++_count}.jpg");
        }

        public byte[] WatchThumbnail { get; set; } = new byte[ClaudeGameHelper.Core.Watch.FrameDiff.Width * ClaudeGameHelper.Core.Watch.FrameDiff.Height];
        public List<(IntPtr Hwnd, string Path)> WatchCaptures { get; } = [];

        public Task<WatchFrame> CaptureForWatchAsync(IntPtr hwnd, string jpgPath)
        {
            if (FailWith != null)
                throw new CaptureFailedException(FailWith);
            WatchCaptures.Add((hwnd, jpgPath));
            return Task.FromResult(new WatchFrame(jpgPath, WatchThumbnail));
        }
    }

    private sealed class FakePanel : IPanelView
    {
        public bool IsShown { get; set; }
        public bool IsActive { get; set; }
        public List<string> Calls { get; } = [];

        public void ShowPassive() { Calls.Add(nameof(ShowPassive)); IsShown = true; }
        public void ShowForAsk() { Calls.Add(nameof(ShowForAsk)); IsShown = true; IsActive = true; }
        public void HidePanel() { Calls.Add(nameof(HidePanel)); IsShown = false; IsActive = false; }
    }

    private readonly FakeAsk _ask = new();
    private readonly FakeCapture _capture = new();
    private readonly FakePanel _panel = new();
    private readonly PanelViewModel _vm = new();
    private readonly List<IntPtr> _focusReturnedTo = [];
    private readonly GameProfile _game;
    private readonly GameProfile _fallback;
    private readonly HelperController _controller;
    private readonly List<GameProfile> _markedNotAGame = [];
    private Func<WindowInfo, GameProfile?> _createAuto = _ => null;

    public HelperControllerTests()
    {
        _game = new GameProfile("game", "Game", TestPaths.NewTempDir(), new WindowMatch(["thegame"], []), [], "Character", null, null, true, false);
        _fallback = new GameProfile("general", "No game", TestPaths.NewTempDir(), null, [], null, null, null, false, true);
        _controller = new HelperController([_game], _fallback, _ask, _capture, _panel, _vm, _focusReturnedTo.Add, OwnPid, w => _createAuto(w), _markedNotAGame.Add);
    }

    private void GameInFront() => _controller.OnForegroundChanged(new WindowInfo(GameHwnd, 77, "thegame", "The Game"));

    [Fact]
    public async Task ScreenSendsScreenQuestionWithOneShotAndShowsPassively()
    {
        GameInFront();

        await _controller.OnScreenHotkeyAsync();

        var request = Assert.Single(_ask.Requests);
        Assert.Equal(PromptBuilder.ScreenQuestion, request.Question);
        Assert.Equal(["shot1.jpg"], request.ScreenshotPaths);
        Assert.False(request.Deep);
        Assert.Same(_game, request.Profile);
        Assert.Equal([GameHwnd], _capture.Captured);
        Assert.Contains("ShowPassive", _panel.Calls);
        Assert.DoesNotContain("ShowForAsk", _panel.Calls);
        Assert.Equal("answer", _vm.Messages[^1].Text);
    }

    [Fact]
    public async Task ScreenInAskModeAddsPendingAndDoesNotSend()
    {
        GameInFront();
        await _controller.OnAskHotkeyAsync();

        await _controller.OnScreenHotkeyAsync();

        Assert.Equal(["shot1.jpg", "shot2.jpg"], _vm.PendingScreenshots);
        Assert.Empty(_ask.Requests);
    }

    [Fact]
    public async Task ScreenWhileBusyDoesNotSend()
    {
        GameInFront();
        _ask.IsBusy = true;

        await _controller.OnScreenHotkeyAsync();

        Assert.Empty(_ask.Requests);
        Assert.Empty(_capture.Captured);
        Assert.Equal("Still answering — wait for it or press Stop.", _vm.StatusText);
    }

    [Fact]
    public async Task ScreenCaptureFailureIsShownAndNothingSent()
    {
        GameInFront();
        _capture.FailWith = "The game window is minimized.";

        await _controller.OnScreenHotkeyAsync();

        Assert.Empty(_ask.Requests);
        Assert.Equal("Couldn't capture the game window: The game window is minimized.", _vm.StatusText);
        Assert.Contains("ShowPassive", _panel.Calls);
    }

    [Fact]
    public async Task AskCapturesIntoPendingAndShowsForAsk()
    {
        GameInFront();
        _vm.IncludeScreenshot = false;

        await _controller.OnAskHotkeyAsync();

        Assert.True(_vm.IsAskMode);
        Assert.True(_vm.IncludeScreenshot);
        Assert.Equal(["shot1.jpg"], _vm.PendingScreenshots);
        Assert.Contains("ShowForAsk", _panel.Calls);
    }

    [Fact]
    public async Task AskAgainWhileAskingDoesNotCaptureAgain()
    {
        GameInFront();
        await _controller.OnAskHotkeyAsync();

        await _controller.OnAskHotkeyAsync();

        Assert.Single(_vm.PendingScreenshots);
    }

    [Fact]
    public async Task AskStillOpensWhenCaptureFails()
    {
        GameInFront();
        _capture.FailWith = "The game window is closed.";

        await _controller.OnAskHotkeyAsync();

        Assert.Empty(_vm.PendingScreenshots);
        Assert.Contains("ShowForAsk", _panel.Calls);
        Assert.Equal("Couldn't capture the game window: The game window is closed.", _vm.StatusText);
    }

    [Fact]
    public async Task SendUsesBoxAndPendingThenHandsFocusBack()
    {
        GameInFront();
        await _controller.OnAskHotkeyAsync();
        await _controller.OnScreenHotkeyAsync();
        _vm.QuestionText = "Is this good?";
        _vm.Deep = true;
        _panel.Calls.Clear();

        await _controller.SendAskBoxAsync();

        var request = Assert.Single(_ask.Requests);
        Assert.Equal("Is this good?", request.Question);
        Assert.Equal(["shot1.jpg", "shot2.jpg"], request.ScreenshotPaths);
        Assert.True(request.Deep);
        Assert.Equal("", _vm.QuestionText);
        Assert.Empty(_vm.PendingScreenshots);
        Assert.False(_vm.IsAskMode);
        Assert.Contains("ShowPassive", _panel.Calls);
        Assert.Equal([GameHwnd], _focusReturnedTo);
    }

    [Fact]
    public async Task SendWithIncludeUncheckedSendsNoShots()
    {
        GameInFront();
        await _controller.OnAskHotkeyAsync();
        _vm.IncludeScreenshot = false;
        _vm.QuestionText = "Best way to farm treasury notes?";

        await _controller.SendAskBoxAsync();

        Assert.Empty(Assert.Single(_ask.Requests).ScreenshotPaths);
    }

    [Fact]
    public async Task EmptyQuestionWithShotsUsesScreenQuestion()
    {
        GameInFront();
        await _controller.OnAskHotkeyAsync();

        await _controller.SendAskBoxAsync();

        Assert.Equal(PromptBuilder.ScreenQuestion, Assert.Single(_ask.Requests).Question);
    }

    [Fact]
    public async Task EmptyQuestionWithoutShotsSendsNothing()
    {
        await _controller.OnAskHotkeyAsync();
        _vm.QuestionText = "   ";

        await _controller.SendAskBoxAsync();

        Assert.Empty(_ask.Requests);
    }

    [Fact]
    public async Task SendWhileBusyKeepsTheQuestion()
    {
        GameInFront();
        await _controller.OnAskHotkeyAsync();
        _vm.QuestionText = "q";
        _ask.IsBusy = true;

        await _controller.SendAskBoxAsync();

        Assert.Empty(_ask.Requests);
        Assert.Equal("q", _vm.QuestionText);
        Assert.Equal("Still answering — wait for it or press Stop.", _vm.StatusText);
    }

    [Fact]
    public void ToggleShowsPassiveThenHides()
    {
        GameInFront();

        _controller.OnToggleHotkey();
        Assert.Equal(["ShowPassive"], _panel.Calls);

        _controller.OnToggleHotkey();
        Assert.Equal(["ShowPassive", "HidePanel"], _panel.Calls);
        Assert.Empty(_focusReturnedTo);
    }

    [Fact]
    public async Task HidingAnActivePanelReturnsFocusToTheGame()
    {
        GameInFront();
        await _controller.OnAskHotkeyAsync();

        _controller.HidePanel();

        Assert.False(_vm.IsAskMode);
        Assert.Equal([GameHwnd], _focusReturnedTo);
    }

    [Fact]
    public void ForegroundOwnProcessKeepsProfile()
    {
        GameInFront();
        var changes = new List<GameProfile>();
        _controller.ActiveProfileChanged += changes.Add;

        _controller.OnForegroundChanged(new WindowInfo(new IntPtr(5), OwnPid, "ClaudeGameHelper", "Claude Game Helper"));

        Assert.Same(_game, _controller.ActiveProfile);
        Assert.Equal(GameHwnd, _controller.GameWindow);
        Assert.Empty(changes);
    }

    [Fact]
    public void ForegroundOtherAppSwitchesToFallback()
    {
        GameInFront();
        var changes = new List<GameProfile>();
        _controller.ActiveProfileChanged += changes.Add;

        _controller.OnForegroundChanged(new WindowInfo(new IntPtr(6), 88, "chrome", "Wiki - Chrome"));

        Assert.Same(_fallback, _controller.ActiveProfile);
        Assert.Equal([_fallback], changes);
        Assert.Equal("No game", _vm.ProfileName);
    }

    [Fact]
    public async Task ActiveTrackingFileFlowsIntoRequest()
    {
        Directory.CreateDirectory(_game.TrackingDir);
        File.WriteAllText(TrackingFiles.FullPath(_game, "vault-dweller"), "");
        GameInFront();

        await _controller.OnScreenHotkeyAsync();

        Assert.Equal("vault-dweller", Assert.Single(_ask.Requests).ActiveTrackingName);
    }

    [Fact]
    public async Task NewTrackingFileBecomesActiveAfterAnswer()
    {
        GameInFront();
        _ask.OnAsk = r =>
        {
            Directory.CreateDirectory(r.Profile.TrackingDir);
            File.WriteAllText(TrackingFiles.FullPath(r.Profile, "x"), "");
        };

        await _controller.OnScreenHotkeyAsync();

        Assert.Equal(["x"], _vm.TrackingNames);
        Assert.Equal("x", _vm.ActiveTrackingName);
    }

    private sealed class QueueContext : SynchronizationContext
    {
        public Queue<(SendOrPostCallback, object?)> Posted { get; } = new();
        public override void Post(SendOrPostCallback d, object? state) => Posted.Enqueue((d, state));
        public void Drain() { while (Posted.TryDequeue(out var item)) item.Item1(item.Item2); }
    }

    private sealed class BackgroundEventsAsk : IAskService
    {
        public bool IsBusy => false;
        public void NewConversation(GameProfile profile) { }
        public void ArchiveNote(GameProfile profile, string note) { }

        public Task<AskOutcome> AskAsync(AskRequest request, Action<ClaudeEvent> onEvent, CancellationToken ct)
        {
            Task.Run(() => onEvent(new TextDelta("partial"))).Wait();
            return Task.FromResult(new AskOutcome("final", false, false, false, "sonnet"));
        }
    }

    [Fact]
    public async Task FinalAnswerIsAppliedAfterStreamedText()
    {
        var context = new QueueContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var vm = new PanelViewModel();
            var controller = new HelperController([_game], _fallback, new BackgroundEventsAsk(), _capture, _panel, vm, _ => { }, OwnPid, _ => null, _ => { });
            controller.OnForegroundChanged(new WindowInfo(GameHwnd, 77, "thegame", "The Game"));

            await controller.OnScreenHotkeyAsync();
            context.Drain();

            Assert.Equal("final", vm.Messages[^1].Text);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private sealed class GatedCapture : IGameCapture
    {
        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Count { get; private set; }

        public async Task<string> CaptureAsync(IntPtr hwnd, string screenshotsDir)
        {
            Count++;
            await Gate.Task;
            return $"shot{Count}.jpg";
        }

        public Task<WatchFrame> CaptureForWatchAsync(IntPtr hwnd, string jpgPath) =>
            Task.FromResult(new WatchFrame(jpgPath, []));
    }

    private sealed class GatedAsk : IAskService
    {
        private bool _inFlight;
        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public bool IsBusy => _inFlight;
        public void NewConversation(GameProfile profile) { }
        public void ArchiveNote(GameProfile profile, string note) { }

        public async Task<AskOutcome> AskAsync(AskRequest request, Action<ClaudeEvent> onEvent, CancellationToken ct)
        {
            Calls++;
            if (_inFlight)
                return new AskOutcome(AskService.BusyMessage, true, false, false, "sonnet");
            _inFlight = true;
            await Gate.Task;
            _inFlight = false;
            return new AskOutcome("answer", false, false, false, "sonnet");
        }
    }

    [Fact]
    public async Task SecondScreenPressDuringCaptureDoesNotDisturbTheFirstAnswer()
    {
        var capture = new GatedCapture();
        var ask = new GatedAsk();
        var vm = new PanelViewModel();
        var controller = new HelperController([_game], _fallback, ask, capture, _panel, vm, _ => { }, OwnPid, _ => null, _ => { });
        controller.OnForegroundChanged(new WindowInfo(GameHwnd, 77, "thegame", "The Game"));

        var first = controller.OnScreenHotkeyAsync();
        var second = controller.OnScreenHotkeyAsync();
        capture.Gate.SetResult();
        await second.WaitAsync(TimeSpan.FromSeconds(5));   // without the guard, the second press waits behind the first
        for (var i = 0; i < 200 && ask.Calls == 0; i++)
            await Task.Delay(10);

        Assert.Equal(1, capture.Count);
        Assert.Equal(1, ask.Calls);
        Assert.True(vm.IsBusy);
        Assert.Equal(2, vm.Messages.Count);

        ask.Gate.SetResult();
        await first;
        // The final answer is posted to the test's synchronization context; give it a moment to apply.
        for (var i = 0; i < 200 && vm.IsBusy; i++)
            await Task.Delay(10);
        Assert.Equal("answer", vm.Messages[^1].Text);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task HidingTheAskBoxDropsItsScreenshotsAndText()
    {
        GameInFront();
        await _controller.OnAskHotkeyAsync();
        _vm.QuestionText = "half-typed";

        _controller.HidePanel();
        await _controller.OnAskHotkeyAsync();

        Assert.Equal(["shot2.jpg"], _vm.PendingScreenshots);
        Assert.Equal("", _vm.QuestionText);
    }

    private GameProfile AutoProfile(string id) =>
        new GameProfile(id, "New Game", TestPaths.NewTempDir(), new WindowMatch([id], []), [], "Notes", null, null, true, false) { IsAuto = true };

    private void NewGameInFront() => _controller.OnForegroundChanged(new WindowInfo(new IntPtr(2002), 99, "newgame", "New Game"));

    [Fact]
    public async Task ScreenOnUnknownGameCreatesAndSwitchesToItsProfile()
    {
        var created = new List<WindowInfo>();
        var auto = AutoProfile("newgame");
        _createAuto = w => { created.Add(w); return auto; };
        var changes = new List<GameProfile>();
        _controller.ActiveProfileChanged += changes.Add;
        NewGameInFront();
        Assert.Same(_fallback, _controller.ActiveProfile);

        await _controller.OnScreenHotkeyAsync();

        Assert.Equal("newgame", Assert.Single(created).ProcessName);
        Assert.Same(auto, _controller.ActiveProfile);
        Assert.Same(auto, Assert.Single(_ask.Requests).Profile);
        Assert.Equal([auto], changes);
        Assert.True(_vm.IsAutoProfile);
    }

    [Fact]
    public async Task AskOnUnknownGameCreatesItsProfileToo()
    {
        var auto = AutoProfile("newgame");
        _createAuto = _ => auto;
        NewGameInFront();

        await _controller.OnAskHotkeyAsync();

        Assert.Same(auto, _controller.ActiveProfile);
    }

    [Fact]
    public void MerelyFocusingAnUnknownWindowCreatesNothing()
    {
        var calls = 0;
        _createAuto = _ => { calls++; return AutoProfile("newgame"); };

        NewGameInFront();

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task CreatedProfileIsMatchedNextTime()
    {
        var calls = 0;
        var auto = AutoProfile("newgame");
        _createAuto = _ => { calls++; return auto; };
        NewGameInFront();
        await _controller.OnScreenHotkeyAsync();

        _controller.OnForegroundChanged(new WindowInfo(new IntPtr(6), 88, "chrome", "Wiki - Chrome"));
        NewGameInFront();
        await _controller.OnScreenHotkeyAsync();

        Assert.Same(auto, _controller.ActiveProfile);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task SkippedWindowStaysOnTheFallback()
    {
        NewGameInFront();

        await _controller.OnScreenHotkeyAsync();

        Assert.Same(_fallback, _controller.ActiveProfile);
        Assert.Same(_fallback, Assert.Single(_ask.Requests).Profile);
    }

    [Fact]
    public async Task NotAGameSwitchesToFallbackAndRemembers()
    {
        var auto = AutoProfile("newgame");
        _createAuto = _ => auto;
        NewGameInFront();
        await _controller.OnScreenHotkeyAsync();
        _createAuto = _ => null;

        _controller.NotAGame();

        Assert.Equal([auto], _markedNotAGame);
        Assert.Same(_fallback, _controller.ActiveProfile);
        Assert.False(_vm.IsAutoProfile);
        Assert.StartsWith("Okay, New Game won't get a profile.", _vm.Messages[^1].Text);
        _controller.OnForegroundChanged(new WindowInfo(new IntPtr(6), 88, "chrome", "Wiki - Chrome"));
        NewGameInFront();
        Assert.Same(_fallback, _controller.ActiveProfile);
    }

    [Fact]
    public async Task SameAutoProfileReturnedTwiceIsNotDuplicated()
    {
        _createAuto = _ => AutoProfile("newgame");   // a fresh instance with the same id each time
        NewGameInFront();
        await _controller.OnScreenHotkeyAsync();
        _controller.OnForegroundChanged(new WindowInfo(new IntPtr(6), 88, "chrome", "Wiki - Chrome"));
        _controller.OnForegroundChanged(new WindowInfo(new IntPtr(2003), 99, "other", "Other"));
        await _controller.OnAskHotkeyAsync();         // fallback + unknown window → create returns "newgame" again
        _controller.HidePanel();

        NewGameInFront();
        _controller.NotAGame();
        _createAuto = _ => null;
        _controller.OnForegroundChanged(new WindowInfo(new IntPtr(6), 88, "chrome", "Wiki - Chrome"));
        NewGameInFront();

        Assert.Same(_fallback, _controller.ActiveProfile);
    }

    [Fact]
    public void NotAGameIgnoredForBuiltIns()
    {
        GameInFront();

        _controller.NotAGame();

        Assert.Empty(_markedNotAGame);
        Assert.Same(_game, _controller.ActiveProfile);
    }

    [Fact]
    public async Task NewConversationResetsAndSaysSo()
    {
        GameInFront();

        _controller.NewConversation();

        Assert.Equal([_game], _ask.NewConversations);
        Assert.Equal("New conversation started.", _vm.Messages[^1].Text);
        await Task.CompletedTask;
    }
}
