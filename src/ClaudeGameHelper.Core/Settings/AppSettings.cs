using System.Text.Json.Serialization;

namespace ClaudeGameHelper.Core.Settings;

public sealed record PanelBounds(double X, double Y, double Width, double Height);

/// <summary>The app's settings.json. Every property has a working default.</summary>
public sealed record AppSettings
{
    public string? ClaudePath { get; init; }

    /// <summary>Where notes, screenshots, and conversations go. Default: Documents\Claude Game Helper.</summary>
    public string? DataFolder { get; init; }

    /// <summary>Programs the person said aren't games (automatic-profile keys).</summary>
    public IReadOnlyList<string> NotGames { get; init; } = [];

    /// <summary>Retired v1 key, still accepted so old settings files load; never written back.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProfilesFolder { get; init; }

    public string ScreenHotkey { get; init; } = "Ctrl+Shift+F1";
    public string AskHotkey { get; init; } = "Ctrl+Shift+F2";
    public string ToggleHotkey { get; init; } = "Ctrl+Shift+F3";
    public string FastModel { get; init; } = "sonnet";
    public string FastEffort { get; init; } = "low";
    public string DeepModel { get; init; } = "opus";
    public string DeepEffort { get; init; } = "medium";
    public int RolloverContextTokens { get; init; } = 100_000;
    public int ScreenshotMaxLongEdge { get; init; } = 1568;
    public int ScreenshotJpegQuality { get; init; } = 90;
    public PanelBounds? Panel { get; init; }
    public Watch.WatchSettings Watch { get; init; } = new();
}

public sealed record SettingsLoadResult(AppSettings Settings, string? Error);
