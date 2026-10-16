namespace ClaudeGameHelper.Core.Profiles;

public static class ProfileMatcher
{
    /// <summary>
    /// Finds the profile for a window: any process-name match wins; otherwise an exact (case-insensitive)
    /// title match. Returns null when nothing matches.
    /// </summary>
    public static GameProfile? Match(IReadOnlyList<GameProfile> profiles, string processName, string windowTitle)
    {
        var process = Path.GetFileNameWithoutExtension(processName.Trim());
        var title = windowTitle.Trim();

        return profiles.FirstOrDefault(p => p.WindowMatch?.ProcessNames.Any(n => string.Equals(n, process, StringComparison.OrdinalIgnoreCase)) == true)
            ?? profiles.FirstOrDefault(p => p.WindowMatch?.Titles.Any(t => string.Equals(t, title, StringComparison.OrdinalIgnoreCase)) == true);
    }
}
