using ClaudeGameHelper.Core;
using ClaudeGameHelper.Core.Claude;
using ClaudeGameHelper.Core.Conversations;
using ClaudeGameHelper.Core.Profiles;
using ClaudeGameHelper.Core.Watch;
using ClaudeGameHelper.Panel;
using ClaudeGameHelper.Platform;

namespace ClaudeGameHelper;

/// <summary>What the controller needs for watch mode. <paramref name="Now"/> defaults to the clock.</summary>
public sealed record WatchDependencies(
    IWatchService Service,
    Func<WatchSettings> Settings,
    Action Chime,
    Action<bool> SetAttention,
    Func<DateTime>? Now = null);

/// <summary>
/// What the hotkeys and the panel's buttons do: tracks which game is in front, captures it, and sends
/// questions through <see cref="IAskService"/> into the panel.
/// </summary>
public sealed class HelperController
{
    private readonly List<GameProfile> _profiles;
    private readonly GameProfile _fallback;
    private readonly Func<WindowInfo, GameProfile?> _createAutoProfile;
    private readonly Action<GameProfile> _markNotAGame;
    private WindowInfo? _lastWindow;
    private readonly IAskService _ask;
    private readonly IGameCapture _capture;
    private readonly IPanelView _panel;
    private readonly PanelViewModel _vm;
    private readonly Action<IntPtr> _returnFocus;
    private readonly int _ownProcessId;
    private readonly SynchronizationContext? _ui;
    private CancellationTokenSource? _cts;
    private bool _sending;
    private readonly WatchDependencies? _watch;
    private CancellationTokenSource? _watchCts;
    private bool _watchRunning;
    private bool _gameInFront;
    private int _watchSent;
    private int _watchSkipped;

    public HelperController(IReadOnlyList<GameProfile> profiles, GameProfile fallback, IAskService ask, IGameCapture capture,
        IPanelView panel, PanelViewModel vm, Action<IntPtr> returnFocus, int ownProcessId,
        Func<WindowInfo, GameProfile?> createAutoProfile, Action<GameProfile> markNotAGame,
        WatchDependencies? watch = null)
    {
        _watch = watch;
        _profiles = [.. profiles];
        _fallback = fallback;
        _createAutoProfile = createAutoProfile;
        _markNotAGame = markNotAGame;
        _ask = ask;
        _capture = capture;
        _panel = panel;
        _vm = vm;
        _returnFocus = returnFocus;
        _ownProcessId = ownProcessId;
        _ui = SynchronizationContext.Current;

        ActiveProfile = fallback;
        vm.SwitchProfile(fallback, TrackingFiles.List(fallback));
    }

    public GameProfile ActiveProfile { get; private set; }

    /// <summary>The last foreground window that wasn't this app: what the hotkeys capture.</summary>
    public IntPtr GameWindow { get; private set; }

    public event Action<GameProfile>? ActiveProfileChanged;

    public void OnForegroundChanged(WindowInfo window)
    {
        // Our own panel in front means the game isn't (and the person may be typing).
        _gameInFront = window.ProcessId != _ownProcessId;
        if (window.ProcessId == _ownProcessId)
            return;

        GameWindow = window.Hwnd;
        _lastWindow = window;
        SwitchTo(ProfileMatcher.Match(_profiles, window.ProcessName, window.Title) ?? _fallback);
    }

    /// <summary>For an automatic profile: the person says this program isn't a game.</summary>
    public void NotAGame()
    {
        var profile = ActiveProfile;
        if (!profile.IsAuto)
            return;

        _markNotAGame(profile);
        _profiles.Remove(profile);
        SwitchTo(_fallback);
        _vm.AddNote($"Okay, {profile.DisplayName} won't get a profile. Its notes are kept in {profile.FolderPath}.");
    }

    public async Task OnScreenHotkeyAsync()
    {
        EnsureAutoProfile();
        if (_vm.IsAskMode)
        {
            if (await CaptureAsync() is { } extra)
                _vm.AddPending(extra);
            return;
        }

        _panel.ShowPassive();
        if (_sending || _ask.IsBusy)
        {
            _vm.StatusText = AskService.BusyMessage;
            return;
        }

        // Claim the slot before the capture's await, so a second press during the capture is refused here.
        _sending = true;
        try
        {
            if (await CaptureAsync() is { } shot)
                await SendAsync(PromptBuilder.ScreenQuestion, [shot], deep: false);
        }
        finally
        {
            _sending = false;
        }
    }

    public async Task OnAskHotkeyAsync()
    {
        EnsureAutoProfile();
        if (_vm.IsAskMode && _panel.IsShown)
        {
            _panel.ShowForAsk();
            return;
        }

        if (await CaptureAsync() is { } shot)
            _vm.AddPending(shot);
        _vm.IsAskMode = true;
        _vm.IncludeScreenshot = true;
        _panel.ShowForAsk();
    }

    public void OnToggleHotkey()
    {
        if (_panel.IsShown)
            HidePanel();
        else
            _panel.ShowPassive();
    }

    public async Task SendAskBoxAsync()
    {
        var question = _vm.QuestionText.Trim();
        var shots = _vm.IncludeScreenshot ? _vm.PendingScreenshots.ToList() : [];
        if (question.Length == 0 && shots.Count == 0)
            return;

        if (_sending || _ask.IsBusy)
        {
            _vm.StatusText = AskService.BusyMessage;
            return;
        }

        var deep = _vm.Deep;
        _vm.ClearAsk();
        _vm.IsAskMode = false;
        _panel.ShowPassive();
        _returnFocus(GameWindow);
        _sending = true;
        try
        {
            await SendAsync(question.Length == 0 ? PromptBuilder.ScreenQuestion : question, shots, deep);
        }
        finally
        {
            _sending = false;
        }
    }

    public void Stop() => _cts?.Cancel();

    public void HidePanel()
    {
        var wasActive = _panel.IsActive;
        _panel.HidePanel();
        if (_vm.IsAskMode)
            _vm.ClearAsk();   // a dismissed question shouldn't ride along with the next one
        _vm.IsAskMode = false;
        if (wasActive)
            _returnFocus(GameWindow);
    }

    public void NewConversation()
    {
        _ask.NewConversation(ActiveProfile);
        _vm.AddNote("New conversation started.");
    }

    /// <summary>
    /// One watch-mode tick (the app calls this every few seconds): decide whether a check is due, capture the
    /// game, skip it if the screen barely changed, otherwise ask Claude and show anything worth showing.
    /// </summary>
    public async Task OnWatchTickAsync()
    {
        if (_watch == null || _watchRunning)
            return;

        _watchRunning = true;
        try
        {
            var profile = ActiveProfile;
            var settings = _watch.Settings();
            var switches = settings.For(profile.Id);
            var state = _watch.Service.For(profile);
            var now = Now();
            var decision = state.Decide(new WatchInputs(now, switches, profile.AllowWatchMode && !profile.IsFallback,
                _gameInFront, _vm.IsAskMode, _ask.IsBusy || _sending, settings.CheckEverySeconds));
            RefreshWatchGoal();
            if (decision != WatchDecision.Check)
                return;

            WatchFrame frame;
            try
            {
                frame = await _capture.CaptureForWatchAsync(GameWindow, Path.Combine(profile.ScreenshotsDir, "watch.jpg"));
            }
            catch (CaptureFailedException ex)
            {
                // Minimized or gone: try again next time, quietly.
                state.MarkChecked(now);
                Log.Info($"Watch capture skipped: {ex.Message}");
                return;
            }

            if (!state.FrameChanged(frame.Thumbnail))
            {
                state.MarkChecked(now);
                Log.Info($"Watch check skipped: screen unchanged ({_watchSent} sent, {++_watchSkipped} skipped this session).");
                return;
            }

            Log.Info($"Watch check sent ({++_watchSent} sent, {_watchSkipped} skipped this session).");
            using var cts = new CancellationTokenSource();
            _watchCts = cts;
            WatchDisplay? display;
            try
            {
                display = await _watch.Service.CheckAsync(profile, frame.Path, switches, _vm.ActiveTrackingName, cts.Token);
            }
            finally
            {
                if (ReferenceEquals(_watchCts, cts))
                    _watchCts = null;
            }

            // A question asked meanwhile canceled this check; its result is stale.
            if (!cts.IsCancellationRequested && display != null)
                ShowWatch(profile, display);
            RefreshWatchGoal();
        }
        finally
        {
            _watchRunning = false;
        }
    }

    /// <summary>✕ Stop in the panel: stop following the current goal.</summary>
    public void StopWatching()
    {
        _watch?.Service.For(ActiveProfile).StopGoal();
        _vm.WatchGoalText = null;
    }

    private DateTime Now() => _watch?.Now?.Invoke() ?? DateTime.Now;

    private void ShowWatch(GameProfile profile, WatchDisplay display)
    {
        _vm.AddWatchMessage(display.Kind, display.Text);
        _ask.ArchiveNote(profile, $"👀 Watch: {display.Text}");

        switch (display.Kind)
        {
            case WatchMessageKind.GoalAlert:
                _panel.ShowPassive();   // time-sensitive: the panel slides in without taking focus
                _watch!.Chime();
                break;
            case WatchMessageKind.Tip:
                _watch!.Chime();
                if (!_panel.IsShown)
                    _watch.SetAttention(true);
                break;
        }
    }

    private void RefreshWatchGoal()
    {
        if (_watch == null)
            return;
        var following = _watch.Settings().For(ActiveProfile.Id).FollowGoal;
        _vm.WatchGoalText = following ? _watch.Service.For(ActiveProfile).Goal?.Label : null;
    }

    /// <summary>
    /// A hotkey used on a window with no profile: make one for it (unless it's Windows itself, a browser, or
    /// something the person said isn't a game). Only hotkeys create profiles, never just focusing a window.
    /// </summary>
    private void EnsureAutoProfile()
    {
        if (!ActiveProfile.IsFallback || _lastWindow == null)
            return;
        if (_createAutoProfile(_lastWindow) is not { } created)
            return;

        // A title variant of a game we already have ("… - Multiplayer") comes back with the same id.
        var existing = _profiles.FirstOrDefault(p => p.Id == created.Id);
        if (existing == null)
            _profiles.Add(created);
        SwitchTo(existing ?? created);
    }

    private void SwitchTo(GameProfile profile)
    {
        if (profile.Id == ActiveProfile.Id)
            return;

        ActiveProfile = profile;
        _vm.SwitchProfile(profile, TrackingFiles.List(profile));
        RefreshWatchGoal();
        ActiveProfileChanged?.Invoke(profile);
    }

    private async Task<string?> CaptureAsync()
    {
        if (GameWindow == IntPtr.Zero)
            return null;

        try
        {
            return await _capture.CaptureAsync(GameWindow, ActiveProfile.ScreenshotsDir);
        }
        catch (CaptureFailedException ex)
        {
            _vm.StatusText = $"Couldn't capture the game window: {ex.Message}";
            Log.Info(_vm.StatusText);
            return null;
        }
    }

    private async Task SendAsync(string question, IReadOnlyList<string> shots, bool deep)
    {
        var profile = ActiveProfile;
        _watchCts?.Cancel();   // his question always wins over a watch check
        var watchState = _watch?.Service.For(profile);
        var answer = _vm.BeginExchange(question, shots);
        using var cts = new CancellationTokenSource();
        _cts = cts;

        AskOutcome outcome;
        try
        {
            outcome = await _ask.AskAsync(
                new AskRequest(profile, question, shots, deep, _vm.ActiveTrackingName, watchState?.RecentMessages.ToList()),
                e => OnUi(() => _vm.Apply(answer, e)), cts.Token);
        }
        catch (Exception ex)
        {
            Log.Error("Asking Claude failed.", ex);
            outcome = new AskOutcome($"Something went wrong: {ex.Message}", true, false, false, "");
        }
        finally
        {
            if (ReferenceEquals(_cts, cts))
                _cts = null;
        }

        if (watchState != null && !outcome.IsError && !outcome.Canceled)
        {
            var followGoal = _watch!.Settings().For(profile.Id).FollowGoal;
            watchState.RecordExchange(question, outcome.AnswerText, question == PromptBuilder.ScreenQuestion, followGoal, Now());
            RefreshWatchGoal();
        }

        OnUi(() =>
        {
            _vm.Finish(answer, outcome);
            if (ActiveProfile.Id == profile.Id)
                _vm.UpdateTrackingNames(TrackingFiles.List(profile));
        });
    }

    /// <summary>
    /// Runs on the UI thread. Always queued (never inline) when there is a UI context, so the final answer
    /// lands after any streamed text still waiting in the queue.
    /// </summary>
    private void OnUi(Action action)
    {
        if (_ui == null)
            action();
        else
            _ui.Post(_ => action(), null);
    }
}
