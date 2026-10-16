using System.Text.Json;

namespace ClaudeGameHelper.Core.Profiles;

/// <summary>
/// Loads the profiles: built-in ones that ship with the app (instructions only), plus automatic ones the
/// helper created in the data root. Every profile's data (notes, screenshots, conversations) lives in
/// <c>&lt;dataRoot&gt;\&lt;id&gt;</c>. Bad profiles are reported, not thrown.
/// </summary>
public static class ProfileLoader
{
    public const string TemplateFolder = "_template";
    private const string Instructions = "CLAUDE.md";

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    public static ProfileLoadResult LoadAll(string builtInRoot, string dataRoot, IReadOnlyCollection<string> notGames)
    {
        var profiles = new List<GameProfile>();
        var errors = new List<string>();
        GameProfile? fallback = null;
        var templatePath = Path.Combine(builtInRoot, TemplateFolder, Instructions);

        foreach (var dir in Folders(builtInRoot).Where(d => Path.GetFileName(d) != TemplateFolder))
        {
            var id = Path.GetFileName(dir);
            if (Read(dir, id, errors) is not { } file)
                continue;

            var builtInInstructions = Path.Combine(dir, Instructions);
            if (!File.Exists(builtInInstructions))
            {
                errors.Add($"Profile \"{id}\": CLAUDE.md is missing.");
                continue;
            }

            var profile = ToProfile(id, file, dataRoot, builtInInstructions);
            if (profile.IsFallback)
                fallback ??= profile;
            else
                profiles.Add(profile);
        }

        var builtInIds = profiles.Select(p => p.Id).Append(fallback?.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in Folders(dataRoot))
        {
            var id = Path.GetFileName(dir);
            if (builtInIds.Contains(id) || notGames.Contains(id, StringComparer.OrdinalIgnoreCase))
                continue;
            if (Read(dir, id, errors) is { Auto: true } file)
                profiles.Add(ToProfile(id, file, dataRoot, templatePath) with { IsAuto = true });
        }

        if (fallback == null)
            errors.Add("No fallback profile found (a built-in profile with \"isFallback\": true, normally \"general\").");

        return new ProfileLoadResult(profiles, fallback, errors, templatePath);
    }

    /// <summary>Loads one automatic profile from <c>&lt;dataRoot&gt;\&lt;id&gt;</c>; null if it isn't one.</summary>
    internal static GameProfile? LoadAuto(string dataRoot, string id, string templatePath) =>
        Read(Path.Combine(dataRoot, id), id, []) is { Auto: true } file
            ? ToProfile(id, file, dataRoot, templatePath) with { IsAuto = true }
            : null;

    private static IEnumerable<string> Folders(string root) =>
        Directory.Exists(root) ? Directory.EnumerateDirectories(root).Order(StringComparer.OrdinalIgnoreCase) : [];

    /// <summary>Reads <c>profile.json</c> from a folder; null (with an error if it's broken) when there isn't a usable one.</summary>
    private static ProfileFile? Read(string dir, string id, List<string> errors)
    {
        var jsonPath = Path.Combine(dir, "profile.json");
        if (!File.Exists(jsonPath))
            return null;

        try
        {
            var file = JsonSerializer.Deserialize<ProfileFile>(File.ReadAllText(jsonPath), JsonOptions);
            if (file == null)
                errors.Add($"Profile \"{id}\": profile.json is empty.");
            return file;
        }
        catch (JsonException ex)
        {
            errors.Add($"Profile \"{id}\": profile.json is not valid JSON ({ex.Message}).");
            return null;
        }
    }

    private static GameProfile ToProfile(string id, ProfileFile file, string dataRoot, string defaultInstructions)
    {
        var folder = Path.GetFullPath(Path.Combine(dataRoot, id));
        var personal = Path.Combine(folder, Instructions);
        return new GameProfile(
            id,
            file.DisplayName ?? id,
            folder,
            file.WindowMatch == null ? null : new WindowMatch(file.WindowMatch.ProcessNames ?? [], file.WindowMatch.Titles ?? []),
            (file.ReadOnlyPaths ?? []).Select(ExpandPath).ToList(),
            file.TrackingLabel,
            file.Hotkeys,
            file.Models,
            file.AllowWatchMode,
            file.IsFallback)
        {
            InstructionsPath = File.Exists(personal) ? personal : defaultInstructions,
        };
    }

    private static string ExpandPath(string path) =>
        Environment.ExpandEnvironmentVariables(
            path.Replace("{Documents}", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), StringComparison.OrdinalIgnoreCase));

    internal sealed record WindowMatchFile(List<string>? ProcessNames, List<string>? Titles);

    internal sealed record ProfileFile(
        string? DisplayName,
        WindowMatchFile? WindowMatch,
        List<string>? ReadOnlyPaths,
        string? TrackingLabel,
        HotkeyOverrides? Hotkeys,
        ModelOverrides? Models,
        bool AllowWatchMode,
        bool IsFallback,
        bool Auto);
}
