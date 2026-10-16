namespace ClaudeGameHelper.Tests;

internal static class TestPaths
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string BuiltInProfilesRoot => Path.Combine(RepoRoot, "src", "ClaudeGameHelper", "BuiltInProfiles");

    public static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    public static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cgh-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ClaudeGameHelper.slnx")))
                return dir.FullName;
        }
        throw new InvalidOperationException("Repo root (ClaudeGameHelper.slnx) not found above " + AppContext.BaseDirectory);
    }
}
