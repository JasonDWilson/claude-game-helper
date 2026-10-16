using ClaudeGameHelper.Core.Claude;
using ClaudeGameHelper.Core.Conversations;
using ClaudeGameHelper.Core.Profiles;
using ClaudeGameHelper.Panel;

namespace ClaudeGameHelper.Tests.Panel;

public class PanelViewModelTests
{
    private static GameProfile Profile(string id, string? trackingLabel = "Character") =>
        new(id, id.ToUpperInvariant(), @"C:\" + id, null, [], trackingLabel, null, null, false, false);

    private readonly PanelViewModel _vm = new();

    public PanelViewModelTests() => _vm.SwitchProfile(Profile("a"), []);

    [Fact]
    public void BeginExchangeAddsQuestionAndEmptyAnswerAndGoesBusy()
    {
        var answer = _vm.BeginExchange("Is this good?", ["a.jpg"]);

        Assert.Equal(2, _vm.Messages.Count);
        Assert.Equal(ChatRole.You, _vm.Messages[0].Role);
        Assert.Equal("Is this good?", _vm.Messages[0].Text);
        Assert.Equal(["a.jpg"], _vm.Messages[0].Screenshots);
        Assert.Same(answer, _vm.Messages[1]);
        Assert.Equal(ChatRole.Claude, answer.Role);
        Assert.Equal("", answer.Text);
        Assert.True(_vm.IsBusy);
        Assert.Equal("Working…", _vm.StatusText);
    }

    [Fact]
    public void TextDeltasStreamThenResultReplaces()
    {
        var a = _vm.BeginExchange("q", []);
        _vm.Apply(a, new TextDelta("Let me check… "));
        _vm.Apply(a, new TextDelta("Scrip"));
        Assert.Equal("Let me check… Scrip", a.Text);

        _vm.Finish(a, new AskOutcome("Scrip it.", false, false, false, "sonnet"));

        Assert.Equal("Scrip it.", a.Text);
        Assert.Equal("sonnet", a.ModelLabel);
        Assert.False(a.IsError);
        Assert.False(_vm.IsBusy);
        Assert.Null(_vm.StatusText);
    }

    [Fact]
    public void ActivitySetsStatus()
    {
        var a = _vm.BeginExchange("q", []);

        _vm.Apply(a, new Activity("Searching the web: x"));

        Assert.Equal("Searching the web: x", _vm.StatusText);
    }

    [Fact]
    public void UsageLimitSetsStatus()
    {
        var a = _vm.BeginExchange("q", []);
        var resets = new DateTimeOffset(2026, 10, 16, 23, 30, 0, TimeSpan.Zero);

        _vm.Apply(a, new UsageLimit(resets));

        Assert.Equal($"Claude usage limit reached — resets at {resets.ToLocalTime():h:mm tt}", _vm.StatusText);
    }

    [Fact]
    public void ErrorOutcomeMarksMessage()
    {
        var a = _vm.BeginExchange("q", []);

        _vm.Finish(a, new AskOutcome("You've hit your limit · resets 3pm", true, false, false, "sonnet"));

        Assert.True(a.IsError);
        Assert.Equal("You've hit your limit · resets 3pm", a.Text);
    }

    [Fact]
    public void RolloverAddsNoteBeforeTheQuestion()
    {
        var a = _vm.BeginExchange("q", []);

        _vm.Finish(a, new AskOutcome("ok", false, false, true, "sonnet"));

        Assert.Equal(3, _vm.Messages.Count);
        Assert.Equal(ChatRole.Note, _vm.Messages[0].Role);
        Assert.Equal("Started a fresh conversation (the last one got long). Your tracking files carry over.", _vm.Messages[0].Text);
        Assert.Equal(ChatRole.You, _vm.Messages[1].Role);
    }

    [Fact]
    public void AddingAScreenshotRechecksInclude()
    {
        _vm.IncludeScreenshot = false;

        _vm.AddPending("a.jpg");

        Assert.True(_vm.IncludeScreenshot);
        Assert.Equal(["a.jpg"], _vm.PendingScreenshots);
    }

    [Fact]
    public void RemovePendingAndClearAsk()
    {
        _vm.AddPending("a.jpg");
        _vm.AddPending("b.jpg");
        _vm.RemovePending("a.jpg");
        Assert.Equal(["b.jpg"], _vm.PendingScreenshots);

        _vm.QuestionText = "x";
        _vm.Deep = true;
        _vm.ClearAsk();

        Assert.Empty(_vm.PendingScreenshots);
        Assert.Equal("", _vm.QuestionText);
        Assert.False(_vm.Deep);
        Assert.True(_vm.IncludeScreenshot);
    }

    [Fact]
    public void HistoriesAreKeptPerProfile()
    {
        _vm.AddNote("in a");
        _vm.SwitchProfile(Profile("b"), []);
        Assert.Empty(_vm.Messages);
        Assert.Equal("B", _vm.ProfileName);

        _vm.SwitchProfile(Profile("a"), []);

        Assert.Equal("in a", Assert.Single(_vm.Messages).Text);
        Assert.Equal("A", _vm.ProfileName);
    }

    [Fact]
    public void ActiveTrackingDefaultsToOnlyFile()
    {
        _vm.SwitchProfile(Profile("b"), ["vault-dweller"]);
        Assert.Equal("vault-dweller", _vm.ActiveTrackingName);
        Assert.Equal("Character", _vm.TrackingLabel);

        _vm.SwitchProfile(Profile("c"), ["one", "two"]);
        Assert.Null(_vm.ActiveTrackingName);
    }

    [Fact]
    public void ActiveTrackingIsRememberedPerProfile()
    {
        _vm.SwitchProfile(Profile("b"), ["one", "two"]);
        _vm.ActiveTrackingName = "two";
        _vm.SwitchProfile(Profile("c"), []);

        _vm.SwitchProfile(Profile("b"), ["one", "two"]);

        Assert.Equal("two", _vm.ActiveTrackingName);
    }

    [Fact]
    public void IsAutoProfileFollowsTheProfile()
    {
        _vm.SwitchProfile(Profile("auto") with { IsAuto = true }, []);
        Assert.True(_vm.IsAutoProfile);

        _vm.SwitchProfile(Profile("b"), []);
        Assert.False(_vm.IsAutoProfile);
    }

    [Fact]
    public void NewTrackingNameIsSelectedWhenNoneActive()
    {
        _vm.UpdateTrackingNames(["vault-dweller"]);
        Assert.Equal("vault-dweller", _vm.ActiveTrackingName);

        _vm.UpdateTrackingNames(["vault-dweller", "zed"]);
        Assert.Equal("vault-dweller", _vm.ActiveTrackingName);
        Assert.Equal(["vault-dweller", "zed"], _vm.TrackingNames);
    }
}
