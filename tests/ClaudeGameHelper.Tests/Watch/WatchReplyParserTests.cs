using ClaudeGameHelper.Core.Watch;

namespace ClaudeGameHelper.Tests.Watch;

public class WatchReplyParserTests
{
    [Theory]
    [InlineData("""{"verdict":"goal_alert","message":"Wrong way. Turn around."}""", WatchVerdict.GoalAlert, "Wrong way. Turn around.")]
    [InlineData("""{"verdict":"tip","message":" Legendary enemy to the left. "}""", WatchVerdict.Tip, "Legendary enemy to the left.")]
    [InlineData("""{"verdict":"goal_reached","message":"You're there."}""", WatchVerdict.GoalReached, "You're there.")]
    [InlineData("""{"verdict":"no_goal","message":""}""", WatchVerdict.NoGoal, "")]
    [InlineData("""{"verdict":"nothing","message":""}""", WatchVerdict.Nothing, "")]
    public void ParsesVerdicts(string text, WatchVerdict verdict, string message) =>
        Assert.Equal(new WatchReply(verdict, message), WatchReplyParser.Parse(text));

    [Fact]
    public void FindsJsonInsideACodeFenceOrProse() =>
        Assert.Equal(
            new WatchReply(WatchVerdict.Tip, "x"),
            WatchReplyParser.Parse("Here you go:\n```json\n{\"verdict\":\"tip\",\"message\":\"x\"}\n```"));

    [Theory]
    [InlineData("")]
    [InlineData("nothing to report")]
    [InlineData("{bad json")]
    [InlineData("""{"verdict":"party","message":"x"}""")]
    [InlineData("""{"message":"x"}""")]
    public void AnythingElseIsNothing(string text) =>
        Assert.Equal(new WatchReply(WatchVerdict.Nothing, ""), WatchReplyParser.Parse(text));

    [Fact]
    public void TipWithoutMessageIsNothing() =>
        Assert.Equal(WatchVerdict.Nothing, WatchReplyParser.Parse("""{"verdict":"tip","message":" "}""").Verdict);

    [Fact]
    public void NoGoalDropsAnyMessage() =>
        Assert.Equal(new WatchReply(WatchVerdict.NoGoal, ""), WatchReplyParser.Parse("""{"verdict":"no_goal","message":"nothing here"}"""));
}
