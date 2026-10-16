using ClaudeGameHelper.Core.Profiles;

namespace ClaudeGameHelper.Core.Conversations;

/// <summary>The Markdown files Claude keeps in a profile's tracking folder (one per character, for example).</summary>
public static class TrackingFiles
{
    public static IReadOnlyList<string> List(GameProfile profile) =>
        Directory.Exists(profile.TrackingDir)
            ? Directory.EnumerateFiles(profile.TrackingDir, "*.md")
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

    public static string RelativePath(string name) => $"tracking/{name}.md";

    public static string FullPath(GameProfile profile, string name) => Path.Combine(profile.TrackingDir, name + ".md");
}
