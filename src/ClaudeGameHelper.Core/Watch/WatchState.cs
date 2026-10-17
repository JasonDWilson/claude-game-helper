namespace ClaudeGameHelper.Core.Watch;

/// <summary>What the player is trying to find or reach, taken from the question that started it.</summary>
public sealed record WatchGoal(string Label, DateTime SetAt);

public sealed record WatchExchange(string Question, string Answer);

public sealed record WatchInputs(
    DateTime Now,
    WatchSwitches Switches,
    bool Allowed,
    bool GameInFront,
    bool Typing,
    bool AskBusy,
    int CheckEverySeconds);

public enum WatchDecision
{
    Check,
    Off,
    NotAllowed,
    NotInFront,
    Typing,
    AskBusy,
    Paused,
    NothingToWatch,
    NotDue,
}

public enum WatchMessageKind
{
    GoalAlert,
    Tip,
    GoalReached,
    Paused,
}

public sealed record WatchDisplay(WatchMessageKind Kind, string Text);

/// <summary>
/// Watch mode's memory for one game: the current goal, recent questions, what's been said, and when the
/// next check is due. No timers or I/O, so every rule here is testable.
/// </summary>
public sealed class WatchState
{
    public static readonly TimeSpan GoalLifetime = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan TipsOnlyInterval = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan TipSpacing = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan AlertRepeatWindow = TimeSpan.FromMinutes(1);
    private const int MaxLabel = 60;
    private const int Keep = 3;

    private readonly List<WatchExchange> _exchanges = [];
    private readonly List<string> _tips = [];
    private readonly List<string> _messages = [];
    private DateTime? _lastCheck;
    private DateTime? _lastTip;
    private (string Text, DateTime At)? _lastAlert;
    private byte[]? _lastFrame;

    public WatchGoal? Goal { get; private set; }
    public IReadOnlyList<WatchExchange> RecentExchanges => _exchanges;
    public IReadOnlyList<string> TipsGiven => _tips;
    public IReadOnlyList<string> RecentMessages => _messages;
    public DateTime? PausedUntil { get; private set; }

    /// <summary>Remembers an answered question; a typed question (not the Screen hotkey's) starts or renews the goal.</summary>
    public void RecordExchange(string question, string answer, bool isScreenQuestion, bool followGoal, DateTime now)
    {
        _exchanges.Add(new WatchExchange(question, answer));
        if (_exchanges.Count > Keep)
            _exchanges.RemoveAt(0);

        if (followGoal && !isScreenQuestion)
        {
            var label = question.Trim();
            Goal = new WatchGoal(label.Length > MaxLabel ? label[..MaxLabel] + "…" : label, now);
        }
    }

    public void StopGoal() => Goal = null;

    public WatchDecision Decide(WatchInputs inputs)
    {
        if (PausedUntil is { } until && inputs.Now < until)
            return WatchDecision.Paused;
        if (!inputs.Switches.FollowGoal && !inputs.Switches.GeneralTips)
            return WatchDecision.Off;
        if (!inputs.Allowed)
            return WatchDecision.NotAllowed;
        if (Goal != null && inputs.Now - Goal.SetAt >= GoalLifetime)
            Goal = null;

        var watchingGoal = inputs.Switches.FollowGoal && Goal != null;
        if (!watchingGoal && !inputs.Switches.GeneralTips)
            return WatchDecision.NothingToWatch;
        if (!inputs.GameInFront)
            return WatchDecision.NotInFront;
        if (inputs.Typing)
            return WatchDecision.Typing;
        if (inputs.AskBusy)
            return WatchDecision.AskBusy;

        var interval = TimeSpan.FromSeconds(inputs.CheckEverySeconds);
        if (!watchingGoal && interval < TipsOnlyInterval)
            interval = TipsOnlyInterval;
        return _lastCheck is { } last && inputs.Now - last < interval ? WatchDecision.NotDue : WatchDecision.Check;
    }

    /// <summary>True when the frame differs enough from the last kept one to be worth a check; keeps changed frames.</summary>
    public bool FrameChanged(byte[] thumbnail)
    {
        if (FrameDiff.Difference(_lastFrame, thumbnail) < FrameDiff.SkipBelow)
            return false;
        _lastFrame = thumbnail;
        return true;
    }

    public void MarkChecked(DateTime now) => _lastCheck = now;

    /// <summary>Turns a check's reply into what to show, after the pacing rules; null means show nothing.</summary>
    public WatchDisplay? Apply(WatchReply reply, DateTime now)
    {
        switch (reply.Verdict)
        {
            case WatchVerdict.GoalReached:
                Goal = null;
                return new WatchDisplay(WatchMessageKind.GoalReached, reply.Message);

            case WatchVerdict.NoGoal:
                Goal = null;
                return null;

            case WatchVerdict.Tip:
                if (_lastTip is { } lastTip && now - lastTip < TipSpacing)
                    return null;
                if (_tips.Contains(reply.Message, StringComparer.OrdinalIgnoreCase))
                    return null;
                _lastTip = now;
                _tips.Add(reply.Message);
                return Shown(new WatchDisplay(WatchMessageKind.Tip, reply.Message));

            case WatchVerdict.GoalAlert:
                if (_lastAlert is { } alert && now - alert.At < AlertRepeatWindow
                    && string.Equals(alert.Text, reply.Message, StringComparison.OrdinalIgnoreCase))
                    return null;
                _lastAlert = (reply.Message, now);
                return Shown(new WatchDisplay(WatchMessageKind.GoalAlert, reply.Message));

            default:
                return null;
        }
    }

    public WatchDisplay Pause(DateTime until, string text)
    {
        PausedUntil = until;
        return new WatchDisplay(WatchMessageKind.Paused, text);
    }

    private WatchDisplay Shown(WatchDisplay display)
    {
        _messages.Add(display.Text);
        if (_messages.Count > Keep)
            _messages.RemoveAt(0);
        return display;
    }
}
