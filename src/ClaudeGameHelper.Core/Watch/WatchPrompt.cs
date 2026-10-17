using System.Text;
using ClaudeGameHelper.Core.Conversations;

namespace ClaudeGameHelper.Core.Watch;

public sealed record WatchCheck(
    string GameName,
    string ScreenshotPath,
    WatchSwitches Switches,
    bool GoalActive,
    IReadOnlyList<WatchExchange> RecentExchanges,
    IReadOnlyList<string> TipsGiven,
    string? TrackingLabel,
    string? ActiveTrackingName);

/// <summary>The text of one watch check: what to look at, what to watch for, the tip bar, and the reply format.</summary>
public static class WatchPrompt
{
    public static string Build(WatchCheck check)
    {
        var blocks = new List<string>
        {
            $"WATCH CHECK. You are glancing at the player's screen while they play {check.GameName}. Use only the Read tool, to view the screenshot.",
            $"Screenshot: {check.ScreenshotPath}",
        };

        if (check.GoalActive)
        {
            var sb = new StringBuilder("Their recent questions (oldest first) and your answers:");
            foreach (var exchange in check.RecentExchanges)
                sb.Append("\nQ: ").Append(exchange.Question.Trim()).Append("\nA: ").Append(exchange.Answer.Trim());
            sb.Append("\nWork out what they're trying to find or reach right now. If none of these is something you can watch for on screen (a place, an item, a quest step), reply no_goal.");
            blocks.Add(sb.ToString());
        }

        if (check.Switches.GeneralTips)
        {
            var tied = check.TrackingLabel != null && check.ActiveTrackingName != null
                ? $"tied to their {check.TrackingLabel.ToLowerInvariant()} file's goals or wishlist ({TrackingFiles.RelativePath(check.ActiveTrackingName)}; read it only if a tip depends on it)"
                : "tied to their goals";
            var given = check.TipsGiven.Count == 0 ? "(none)" : string.Join("; ", check.TipsGiven);
            blocks.Add(
                "General tips are on. Only give a tip if it clears this bar: easy to miss and worth having (rare loot or a plan, a legendary enemy, an event starting nearby, a wishlist item on a vendor); " +
                "a mistake about to happen (underleveled for a fight, about to be overencumbered, about to scrap or sell something valuable); " +
                $"or {tied}. Never lore, trivia, generic advice, or a repeat. When unsure, say nothing.\n" +
                $"Tips already given: {given}");
        }

        blocks.Add(check.GoalActive
            ? "Reply with exactly one line of JSON and nothing else:\n" +
              "{\"verdict\":\"nothing|goal_alert|tip|goal_reached|no_goal\",\"message\":\"one or two short sentences, verdict first\"}\n" +
              "goal_alert: they're heading the wrong way or about to miss what they're looking for. goal_reached: they've arrived or found it. Leave message empty for nothing and no_goal."
            : "Reply with exactly one line of JSON and nothing else:\n" +
              "{\"verdict\":\"nothing|tip\",\"message\":\"one or two short sentences, verdict first\"}\n" +
              "Leave message empty for nothing.");

        return string.Join("\n\n", blocks);
    }
}
