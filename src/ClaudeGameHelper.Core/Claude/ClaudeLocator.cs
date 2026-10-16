using System.Diagnostics;

namespace ClaudeGameHelper.Core.Claude;

public sealed record ClaudeLocateResult(string? Path, IReadOnlyList<string> Tried);

public static class ClaudeLocator
{
    private const string ExtensionPrefix = "anthropic.claude-code-";
    private const string ExtensionSuffix = "-win32-x64";

    /// <summary>
    /// Finds a working Claude Code executable: the configured path, then <c>claude.exe</c>/<c>claude.cmd</c>
    /// on PATH, then the newest VS Code extension's bundled binary. Each candidate that exists is probed.
    /// </summary>
    public static ClaudeLocateResult Find(string? configuredPath, string? pathEnv, string vscodeExtensionsDir, Func<string, bool> probe)
    {
        var tried = new List<string>();

        foreach (var candidate in Candidates(configuredPath, pathEnv, vscodeExtensionsDir))
        {
            tried.Add(candidate);
            if (File.Exists(candidate) && probe(candidate))
                return new ClaudeLocateResult(candidate, tried);
        }

        return new ClaudeLocateResult(null, tried);
    }

    /// <summary>True when "<paramref name="path"/> --version" exits 0 within 10 seconds and names Claude Code.</summary>
    public static bool ProbeVersion(string path)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(path, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process == null)
                return false;

            var output = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(10_000))
            {
                process.Kill(entireProcessTree: true);
                return false;
            }

            return process.ExitCode == 0 && output.Result.Contains("Claude Code", StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            Log.Info($"Claude Code probe failed for {path}: {ex.Message}");
            return false;
        }
    }

    private static IEnumerable<string> Candidates(string? configuredPath, string? pathEnv, string vscodeExtensionsDir)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
            yield return configuredPath;

        foreach (var dir in (pathEnv ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var name in new[] { "claude.exe", "claude.cmd" })
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate))
                    yield return candidate;
            }
        }

        if (!Directory.Exists(vscodeExtensionsDir))
            yield break;

        var extensions = Directory.EnumerateDirectories(vscodeExtensionsDir, ExtensionPrefix + "*" + ExtensionSuffix)
            .Select(dir => (Dir: dir, Version: ExtensionVersion(Path.GetFileName(dir))))
            .Where(x => x.Version != null)
            .OrderByDescending(x => x.Version);

        foreach (var (dir, _) in extensions)
        {
            var candidate = Path.Combine(dir, "resources", "native-binary", "claude.exe");
            if (File.Exists(candidate))
                yield return candidate;
        }
    }

    private static Version? ExtensionVersion(string folderName)
    {
        var middle = folderName[ExtensionPrefix.Length..^ExtensionSuffix.Length];
        return Version.TryParse(middle, out var version) ? version : null;
    }
}
