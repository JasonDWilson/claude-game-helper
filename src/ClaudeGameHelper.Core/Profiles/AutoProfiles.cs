using System.Text;
using System.Text.Json;

namespace ClaudeGameHelper.Core.Profiles;

/// <summary>Profiles the helper makes on its own the first time a hotkey is used on a game it doesn't know.</summary>
public static class AutoProfiles
{
    private const int MaxKeyLength = 40;

    /// <summary>Windows itself and web browsers: never treated as games.</summary>
    private static readonly HashSet<string> SkipProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "shellexperiencehost", "startmenuexperiencehost", "searchhost", "searchapp", "textinputhost",
        "lockapp", "chrome", "msedge", "firefox", "opera", "brave", "vivaldi",
    };

    /// <summary>
    /// Programs that run many different games, so the window title tells the games apart: Java (Minecraft
    /// Java Edition and others) and the frame host that Store/Game Pass apps' windows report as.
    /// </summary>
    private static readonly HashSet<string> HostProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "javaw", "java", "applicationframehost",
    };

    /// <summary>The profile id (and data folder name) for a window; null when it should never get a profile.</summary>
    public static string? KeyFor(string processName, string title)
    {
        var process = Path.GetFileNameWithoutExtension(processName.Trim());
        if (process.Length == 0 || SkipProcesses.Contains(process))
            return null;
        if (!HostProcesses.Contains(process))
            return process.ToLowerInvariant();

        var slug = Slug(BaseTitle(title));
        return slug.Length == 0 ? null : slug;
    }

    /// <summary>
    /// The part of a host program's title that names the game: "Minecraft 1.21.1 - Singleplayer" and
    /// "Minecraft 1.21.1 - Multiplayer" are the same game.
    /// </summary>
    private static string BaseTitle(string title)
    {
        var trimmed = title.Trim();
        var dash = trimmed.IndexOf(" - ", StringComparison.Ordinal);
        return (dash > 0 ? trimmed[..dash] : trimmed).Trim();
    }

    /// <summary>
    /// Returns the profile for this window, creating <c>&lt;dataRoot&gt;\&lt;key&gt;\profile.json</c> the first time.
    /// Null when the window is skipped or the person said it isn't a game.
    /// </summary>
    public static GameProfile? TryCreate(string dataRoot, string templatePath, string processName, string title,
        IReadOnlyCollection<string> notGames)
    {
        if (KeyFor(processName, title) is not { } key || notGames.Contains(key, StringComparer.OrdinalIgnoreCase))
            return null;

        var folder = Path.Combine(dataRoot, key);
        var jsonPath = Path.Combine(folder, "profile.json");
        if (!File.Exists(jsonPath))
        {
            var process = Path.GetFileNameWithoutExtension(processName.Trim());
            var byTitle = HostProcesses.Contains(process);
            var shownTitle = byTitle ? BaseTitle(title) : title.Trim();
            var displayName = shownTitle.Length > 0 ? shownTitle : process;
            var file = new ProfileLoader.ProfileFile(
                displayName,
                new ProfileLoader.WindowMatchFile(byTitle ? [] : [process.ToLowerInvariant()], byTitle ? [shownTitle] : []),
                null, "Notes", null, null, AllowWatchMode: true, IsFallback: false, Auto: true);
            Directory.CreateDirectory(folder);
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(file, ProfileLoader.JsonOptions));
            Log.Info($"Created a profile for \"{displayName}\" in {folder}.");
        }

        return ProfileLoader.LoadAuto(dataRoot, key, templatePath);
    }

    private static string Slug(string title)
    {
        var sb = new StringBuilder();
        foreach (var c in title.ToLowerInvariant())
        {
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
                sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-')
                sb.Append('-');
        }

        var slug = sb.ToString().Trim('-');
        return slug.Length > MaxKeyLength ? slug[..MaxKeyLength].TrimEnd('-') : slug;
    }
}
