using ClaudeGameHelper.Core.Watch;

namespace ClaudeGameHelper.Tests.Watch;

public class WatchPromptTests
{
    private static WatchCheck Check(bool goal = true, bool tips = false, IReadOnlyList<string>? tipsGiven = null,
        string? label = "Character", string? active = "vault-dweller") =>
        new("Fallout 76", @"C:\d\fallout-76\screenshots\watch.jpg", new WatchSwitches(goal, tips), GoalActive: goal,
            [new WatchExchange("Where's the Whitespring?", "Head northeast along the road.")],
            tipsGiven ?? [], label, active);

    [Fact]
    public void PromptNamesTheScreenshotAndGame()
    {
        var p = WatchPrompt.Build(Check());

        Assert.StartsWith("WATCH CHECK. You are glancing at the player's screen while they play Fallout 76.", p);
        Assert.Contains(@"Screenshot: C:\d\fallout-76\screenshots\watch.jpg", p);
    }

    [Fact]
    public void PromptHasGoalBlockOnlyWithAGoal()
    {
        var with = WatchPrompt.Build(Check(goal: true));
        var without = WatchPrompt.Build(Check(goal: false, tips: true));

        Assert.Contains("Their recent questions (oldest first) and your answers:\nQ: Where's the Whitespring?\nA: Head northeast along the road.", with);
        Assert.Contains("reply no_goal", with);
        Assert.DoesNotContain("Their recent questions", without);
    }

    [Fact]
    public void PromptHasTipRulesOnlyWhenTipsAreOn()
    {
        var off = WatchPrompt.Build(Check(tips: false));
        var on = WatchPrompt.Build(Check(tips: true, tipsGiven: ["Legendary enemy ahead.", "Plan on the vendor."]));
        var none = WatchPrompt.Build(Check(tips: true));

        Assert.DoesNotContain("Only give a tip", off);
        Assert.Contains("General tips are on. Only give a tip if it clears this bar:", on);
        Assert.Contains("their character file's goals or wishlist (tracking/vault-dweller.md; read it only if a tip depends on it)", on);
        Assert.Contains("Tips already given: Legendary enemy ahead.; Plan on the vendor.", on);
        Assert.Contains("Tips already given: (none)", none);
    }

    [Fact]
    public void TrackingClauseIsLeftOutWithoutAnActiveFile()
    {
        var p = WatchPrompt.Build(Check(tips: true, active: null));

        Assert.Contains("or tied to their goals.", p);
        Assert.DoesNotContain("tracking/", p);
    }

    [Fact]
    public void PromptEndsWithTheReplyFormat()
    {
        var p = WatchPrompt.Build(Check());

        Assert.Contains("Reply with exactly one line of JSON and nothing else:\n{\"verdict\":\"nothing|goal_alert|tip|goal_reached|no_goal\",\"message\":\"one or two short sentences, verdict first\"}", p);
        Assert.EndsWith("Leave message empty for nothing and no_goal.", p);
    }
}
