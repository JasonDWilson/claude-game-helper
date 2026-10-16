namespace ClaudeGameHelper.Core.Profiles;

/// <summary>How to recognize a game's window. Titles match exactly, ignoring case.</summary>
public sealed record WindowMatch(IReadOnlyList<string> ProcessNames, IReadOnlyList<string> Titles);

public sealed record HotkeyOverrides(string? Screen, string? Ask, string? Toggle);

public sealed record ModelOverrides(string? FastModel, string? FastEffort, string? DeepModel, string? DeepEffort);

/// <summary>One game's profile folder: its window match, instructions for Claude, and data folders.</summary>
public sealed record GameProfile(
    string Id,
    string DisplayName,
    string FolderPath,
    WindowMatch? WindowMatch,
    IReadOnlyList<string> ReadOnlyPaths,
    string? TrackingLabel,
    HotkeyOverrides? Hotkeys,
    ModelOverrides? Models,
    bool AllowWatchMode,
    bool IsFallback)
{
    /// <summary>The CLAUDE.md Claude gets: a personal one in the data folder, else the built-in or template.</summary>
    public string InstructionsPath { get; init; } = Path.Combine(FolderPath, "CLAUDE.md");

    /// <summary>True for a profile the helper created on its own for a game it didn't know.</summary>
    public bool IsAuto { get; init; }

    public string TrackingDir => Path.Combine(FolderPath, "tracking");
    public string ScreenshotsDir => Path.Combine(FolderPath, "screenshots");
    public string ConversationsDir => Path.Combine(FolderPath, "conversations");
}

public sealed record ProfileLoadResult(
    IReadOnlyList<GameProfile> Profiles,
    GameProfile? Fallback,
    IReadOnlyList<string> Errors,
    string TemplateInstructionsPath);
