using ClaudeGameHelper.Core.Conversations;
using ClaudeGameHelper.Core.Watch;

namespace ClaudeGameHelper.Tests.Watch;

public class WatchStateTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 20, 0, 0);
    private static readonly WatchSwitches GoalOnly = new(true, false);
    private static readonly WatchSwitches TipsOnly = new(false, true);
    private readonly WatchState _s = new();

    private static WatchInputs At(DateTime now, WatchSwitches? switches = null, bool allowed = true, bool front = true,
        bool typing = false, bool busy = false, int every = 30) =>
        new(now, switches ?? GoalOnly, allowed, front, typing, busy, every);

    private void SetGoal(string question = "Where's the Whitespring?", DateTime? at = null) =>
        _s.RecordExchange(question, "Head northeast.", isScreenQuestion: false, followGoal: true, at ?? Now);

    [Fact]
    public void TypedQuestionSetsGoalScreenQuestionDoesNot()
    {
        _s.RecordExchange(PromptBuilder.ScreenQuestion, "a", isScreenQuestion: true, followGoal: true, Now);
        Assert.Null(_s.Goal);

        SetGoal();

        Assert.Equal(new WatchGoal("Where's the Whitespring?", Now), _s.Goal);
    }

    [Fact]
    public void FollowGoalOffSetsNoGoal()
    {
        _s.RecordExchange("Where's the Whitespring?", "a", false, followGoal: false, Now);

        Assert.Null(_s.Goal);
        Assert.Single(_s.RecentExchanges);
    }

    [Fact]
    public void KeepsLastThreeExchanges()
    {
        foreach (var q in new[] { "one", "two", "three", "four" })
            _s.RecordExchange(q, "a-" + q, false, true, Now);

        Assert.Equal(["two", "three", "four"], _s.RecentExchanges.Select(e => e.Question));
        Assert.Equal("a-four", _s.RecentExchanges[^1].Answer);
    }

    [Fact]
    public void LongQuestionLabelIsCut()
    {
        SetGoal(new string('x', 80));

        Assert.Equal(new string('x', 60) + "…", _s.Goal!.Label);
    }

    [Fact]
    public void NewQuestionResetsGoalClock()
    {
        SetGoal(at: Now);
        SetGoal("Which road?", Now.AddMinutes(25));

        Assert.Equal(WatchDecision.Check, _s.Decide(At(Now.AddMinutes(40))));
        Assert.Equal("Which road?", _s.Goal!.Label);
    }

    [Fact]
    public void FirstCheckIsDueImmediately()
    {
        SetGoal();

        Assert.Equal(WatchDecision.Check, _s.Decide(At(Now)));
    }

    [Theory]
    [InlineData(29, WatchDecision.NotDue)]
    [InlineData(30, WatchDecision.Check)]
    public void GoalChecksFollowTheFrequency(int secondsLater, WatchDecision expected)
    {
        SetGoal();
        _s.MarkChecked(Now);

        Assert.Equal(expected, _s.Decide(At(Now.AddSeconds(secondsLater))));
    }

    [Fact]
    public void TipsOnlyChecksAtMostEveryTwoMinutes()
    {
        _s.MarkChecked(Now);

        Assert.Equal(WatchDecision.NotDue, _s.Decide(At(Now.AddSeconds(60), TipsOnly)));
        Assert.Equal(WatchDecision.Check, _s.Decide(At(Now.AddSeconds(120), TipsOnly)));
    }

    [Fact]
    public void GoalExpiresAfterThirtyMinutes()
    {
        SetGoal();

        Assert.Equal(WatchDecision.NothingToWatch, _s.Decide(At(Now.AddMinutes(30))));
        Assert.Null(_s.Goal);
    }

    [Fact]
    public void ReasonsNotToCheck()
    {
        SetGoal();

        Assert.Equal(WatchDecision.Off, _s.Decide(At(Now, new WatchSwitches(false, false))));
        Assert.Equal(WatchDecision.NotAllowed, _s.Decide(At(Now, allowed: false)));
        Assert.Equal(WatchDecision.NotInFront, _s.Decide(At(Now, front: false)));
        Assert.Equal(WatchDecision.Typing, _s.Decide(At(Now, typing: true)));
        Assert.Equal(WatchDecision.AskBusy, _s.Decide(At(Now, busy: true)));
    }

    [Fact]
    public void GoalModeWithoutAGoalHasNothingToWatch() =>
        Assert.Equal(WatchDecision.NothingToWatch, _s.Decide(At(Now)));

    [Fact]
    public void PausedUntilResetTime()
    {
        SetGoal();

        var note = _s.Pause(Now.AddMinutes(10), "paused");

        Assert.Equal(new WatchDisplay(WatchMessageKind.Paused, "paused"), note);
        Assert.Equal(Now.AddMinutes(10), _s.PausedUntil);
        Assert.Equal(WatchDecision.Paused, _s.Decide(At(Now.AddMinutes(9))));
        Assert.Equal(WatchDecision.Check, _s.Decide(At(Now.AddMinutes(10))));
    }

    [Fact]
    public void FrameChangedComparesWithLastKept()
    {
        var a = new byte[FrameDiff.Width * FrameDiff.Height];
        var b = Enumerable.Repeat((byte)255, a.Length).ToArray();

        Assert.True(_s.FrameChanged(a));
        Assert.False(_s.FrameChanged(a));
        Assert.True(_s.FrameChanged(b));
        Assert.False(_s.FrameChanged(b));
    }

    [Fact]
    public void GoalReachedClearsGoalAndShowsNote()
    {
        SetGoal();

        Assert.Equal(new WatchDisplay(WatchMessageKind.GoalReached, "You're there."),
            _s.Apply(new WatchReply(WatchVerdict.GoalReached, "You're there."), Now));
        Assert.Null(_s.Goal);
    }

    [Fact]
    public void NoGoalClearsGoalSilently()
    {
        SetGoal();

        Assert.Null(_s.Apply(new WatchReply(WatchVerdict.NoGoal, ""), Now));
        Assert.Null(_s.Goal);
    }

    [Fact]
    public void NothingShowsNothing() => Assert.Null(_s.Apply(new WatchReply(WatchVerdict.Nothing, ""), Now));

    [Fact]
    public void TipsArePacedThreeMinutesApart()
    {
        Assert.NotNull(_s.Apply(new WatchReply(WatchVerdict.Tip, "one"), Now));
        Assert.Null(_s.Apply(new WatchReply(WatchVerdict.Tip, "two"), Now.AddMinutes(2)));
        Assert.Equal(new WatchDisplay(WatchMessageKind.Tip, "three"), _s.Apply(new WatchReply(WatchVerdict.Tip, "three"), Now.AddMinutes(3)));
    }

    [Fact]
    public void SameTipNeverRepeats()
    {
        _s.Apply(new WatchReply(WatchVerdict.Tip, "Legendary enemy ahead."), Now);

        Assert.Null(_s.Apply(new WatchReply(WatchVerdict.Tip, "legendary enemy ahead."), Now.AddMinutes(10)));
    }

    [Fact]
    public void SameAlertNotWithinAMinute()
    {
        Assert.NotNull(_s.Apply(new WatchReply(WatchVerdict.GoalAlert, "Wrong way."), Now));
        Assert.Null(_s.Apply(new WatchReply(WatchVerdict.GoalAlert, "Wrong way."), Now.AddSeconds(30)));
        Assert.NotNull(_s.Apply(new WatchReply(WatchVerdict.GoalAlert, "Turn left."), Now.AddSeconds(35)));
        Assert.NotNull(_s.Apply(new WatchReply(WatchVerdict.GoalAlert, "Wrong way."), Now.AddSeconds(61)));
    }

    [Fact]
    public void ShownMessagesAreRemembered()
    {
        _s.Apply(new WatchReply(WatchVerdict.Tip, "tip one"), Now);
        _s.Apply(new WatchReply(WatchVerdict.GoalAlert, "a"), Now.AddMinutes(1));
        _s.Apply(new WatchReply(WatchVerdict.GoalAlert, "b"), Now.AddMinutes(2));
        _s.Apply(new WatchReply(WatchVerdict.Tip, "tip two"), Now.AddMinutes(4));

        Assert.Equal(["a", "b", "tip two"], _s.RecentMessages);
        Assert.Equal(["tip one", "tip two"], _s.TipsGiven);
    }

    [Fact]
    public void WithoutAGoalGoalVerdictsCantOpenThePanel()
    {
        Assert.Equal(new WatchDisplay(WatchMessageKind.Tip, "About to miss a legendary."),
            _s.Apply(new WatchReply(WatchVerdict.GoalAlert, "About to miss a legendary."), Now, goalActive: false));
        Assert.Null(_s.Apply(new WatchReply(WatchVerdict.GoalReached, "You're there."), Now.AddMinutes(5), goalActive: false));
        Assert.Equal(["About to miss a legendary."], _s.TipsGiven);
    }

    [Fact]
    public void StopGoalClearsIt()
    {
        SetGoal();

        _s.StopGoal();

        Assert.Null(_s.Goal);
    }
}
