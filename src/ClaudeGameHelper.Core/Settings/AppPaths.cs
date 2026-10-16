namespace ClaudeGameHelper.Core.Settings;

public static class AppPaths
{
    public static string SettingsFile { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeGameHelper", "settings.json");

    /// <summary>The instructions that ship with the app (replaced on every update).</summary>
    public static string BuiltInProfilesDir { get; } = Path.Combine(AppContext.BaseDirectory, "BuiltInProfiles");

    /// <summary>
    /// Everyone's data: one folder per game. Documents follows OneDrive redirection, so this lands wherever
    /// the person's Documents really is.
    /// </summary>
    public static string DataRoot(string? configured) =>
        string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Claude Game Helper")
            : configured;

    /// <summary>Logs sit with the data, where people can find them to send along when something breaks.</summary>
    public static string LogsDir(string dataRoot) => Path.Combine(dataRoot, "logs");
}
