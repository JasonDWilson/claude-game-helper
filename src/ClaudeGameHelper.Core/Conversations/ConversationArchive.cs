using System.Text;

namespace ClaudeGameHelper.Core.Conversations;

/// <summary>Writes conversations as Markdown for reading later in any editor.</summary>
public static class ConversationArchive
{
    public static void AppendTurn(string archivePath, string profileName, DateTime at, string question,
        IReadOnlyList<string> screenshotPaths, string model, string answer)
    {
        var dir = Path.GetDirectoryName(archivePath)!;
        Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        if (!File.Exists(archivePath))
            sb.Append($"# {profileName} — {at:yyyy-MM-dd HH:mm}\n\n");

        sb.Append($"## You · {at:HH:mm}\n{question.Trim()}\n\n");
        foreach (var path in screenshotPaths)
            sb.Append($"![screenshot]({Path.GetRelativePath(dir, path).Replace('\\', '/')})\n\n");
        sb.Append($"## Claude · {model} · {at:HH:mm}\n{answer.Trim()}\n\n");

        File.AppendAllText(archivePath, sb.ToString());
    }

    /// <summary>Appends an unprompted note, such as a watch-mode alert that was shown.</summary>
    public static void AppendNote(string archivePath, string profileName, DateTime at, string note)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
        var header = File.Exists(archivePath) ? "" : $"# {profileName} — {at:yyyy-MM-dd HH:mm}\n\n";
        File.AppendAllText(archivePath, $"{header}## 👀 Watch · {at:HH:mm}\n{note.Trim()}\n\n");
    }
}
