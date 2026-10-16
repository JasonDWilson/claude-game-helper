namespace ClaudeGameHelper.Tests.Profiles;

public class GameNeutralityTests
{
    [Fact]
    public void SourceNeverNamesAGame()
    {
        var src = Path.Combine(TestPaths.RepoRoot, "src");
        var offenders = Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".xaml"))
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .Where(f => File.ReadAllText(f).Contains("Fallout", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(offenders);
    }
}
