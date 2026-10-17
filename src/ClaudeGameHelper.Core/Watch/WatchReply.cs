using System.Text.Json;

namespace ClaudeGameHelper.Core.Watch;

public enum WatchVerdict
{
    Nothing,
    GoalAlert,
    Tip,
    GoalReached,
    NoGoal,
}

public sealed record WatchReply(WatchVerdict Verdict, string Message);

/// <summary>Reads a watch check's one-line JSON reply. Anything it can't read counts as "nothing to say".</summary>
public static class WatchReplyParser
{
    private static readonly WatchReply Nothing = new(WatchVerdict.Nothing, "");

    public static WatchReply Parse(string resultText)
    {
        var start = resultText.IndexOf('{');
        var end = resultText.LastIndexOf('}');
        if (start < 0 || end <= start)
            return Nothing;

        try
        {
            using var doc = JsonDocument.Parse(resultText[start..(end + 1)]);
            var root = doc.RootElement;
            var verdict = Text(root, "verdict") switch
            {
                "goal_alert" => WatchVerdict.GoalAlert,
                "tip" => WatchVerdict.Tip,
                "goal_reached" => WatchVerdict.GoalReached,
                "no_goal" => WatchVerdict.NoGoal,
                _ => WatchVerdict.Nothing,
            };
            var message = Text(root, "message")?.Trim() ?? "";

            return verdict switch
            {
                WatchVerdict.NoGoal => new WatchReply(WatchVerdict.NoGoal, ""),
                WatchVerdict.Nothing => Nothing,
                _ when message.Length == 0 => Nothing,
                _ => new WatchReply(verdict, message),
            };
        }
        catch (JsonException)
        {
            return Nothing;
        }
    }

    private static string? Text(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
}
