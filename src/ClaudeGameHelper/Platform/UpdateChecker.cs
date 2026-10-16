using ClaudeGameHelper.Core;
using Velopack;
using Velopack.Sources;

namespace ClaudeGameHelper.Platform;

/// <summary>
/// Checks GitHub Releases for a newer version and downloads it quietly; Velopack applies it when the helper
/// exits, so the next start runs the new version. Only installed builds update; failures go to the log.
/// </summary>
public static class UpdateChecker
{
    public const string RepoUrl = "https://github.com/JasonDWilson/claude-game-helper";

    public static Task RunAsync() => Safely(async () =>
    {
        var manager = CreateManager();
        if (!manager.IsInstalled)
            return;

        Log.Info($"Checking for updates (running {manager.CurrentVersion}).");
        var update = await manager.CheckForUpdatesAsync();
        if (update == null)
        {
            Log.Info("No update available.");
            return;
        }

        Log.Info($"Downloading update {update.TargetFullRelease.Version}.");
        await manager.DownloadUpdatesAsync(update);
        manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart: false);
        Log.Info($"Update {update.TargetFullRelease.Version} will be applied when the helper exits.");
    });

    public static async Task Safely(Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            Log.Info($"Update check failed: {ex.Message}");
        }
    }

    internal static UpdateManager CreateManager() => new(new GithubSource(RepoUrl, null, false));
}

public static class AppVersion
{
    /// <summary>The installed version ("0.2.0"), or "dev" when running from a build folder.</summary>
    public static string Current { get; } = Read();

    private static string Read()
    {
        try
        {
            var manager = UpdateChecker.CreateManager();
            return manager.IsInstalled ? manager.CurrentVersion?.ToString() ?? "dev" : "dev";
        }
        catch (Exception)
        {
            return "dev";
        }
    }
}
