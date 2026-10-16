using System.Text;

namespace ClaudeGameHelper.Core.Conversations;

public static class PromptBuilder
{
    public const string ScreenQuestion = "What am I looking at, and what should I do?";

    /// <summary>
    /// The text sent to Claude: the question, the screenshot paths to read, which game it is, and the active
    /// tracking file.
    /// </summary>
    public static string Build(string question, IReadOnlyList<string> screenshotPaths, string? gameName,
        string? trackingLabel, string? activeTrackingName)
    {
        var sb = new StringBuilder(question.Trim());

        if (screenshotPaths.Count > 0)
        {
            sb.Append("\n\nScreenshots of my game screen (view each with the Read tool):");
            foreach (var path in screenshotPaths)
                sb.Append("\n- ").Append(path);
        }

        if (gameName != null)
            sb.Append("\n\nGame: ").Append(gameName);

        if (trackingLabel != null)
        {
            var label = trackingLabel.ToLowerInvariant();
            sb.Append(activeTrackingName == null
                ? $"\n\nNo {label} file is selected yet."
                : $"\n\nActive {label} file: {TrackingFiles.RelativePath(activeTrackingName)}");
        }

        return sb.ToString();
    }
}
