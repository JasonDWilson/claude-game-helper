using ClaudeGameHelper.Core.Claude;

namespace ClaudeGameHelper.Tests.Claude;

public class ClaudeLocatorTests
{
    private readonly string _root = TestPaths.NewTempDir();

    private string Touch(params string[] parts)
    {
        var path = Path.Combine([_root, .. parts]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
        return path;
    }

    private string ExtensionBinary(string version) =>
        Touch("ext", $"anthropic.claude-code-{version}-win32-x64", "resources", "native-binary", "claude.exe");

    [Fact]
    public void ConfiguredPathWinsWhenItWorks()
    {
        var configured = Touch("custom", "claude.exe");
        ExtensionBinary("2.1.295");

        var r = ClaudeLocator.Find(configured, null, Path.Combine(_root, "ext"), _ => true);

        Assert.Equal(configured, r.Path);
    }

    [Fact]
    public void BrokenPathEntryIsSkippedForNewestExtension()
    {
        var cmd = Touch("npm", "claude.cmd");
        ExtensionBinary("2.1.284");
        var newest = ExtensionBinary("2.1.295");

        var r = ClaudeLocator.Find(null, Path.Combine(_root, "npm"), Path.Combine(_root, "ext"), p => p != cmd);

        Assert.Equal(newest, r.Path);
        Assert.EndsWith(@"anthropic.claude-code-2.1.295-win32-x64\resources\native-binary\claude.exe", r.Path);
        Assert.Contains(cmd, r.Tried);
    }

    [Fact]
    public void WorkingPathEntryBeatsExtension()
    {
        var exe = Touch("bin", "claude.exe");
        ExtensionBinary("2.1.295");

        var r = ClaudeLocator.Find(null, $@"C:\nowhere;{Path.Combine(_root, "bin")}", Path.Combine(_root, "ext"), _ => true);

        Assert.Equal(exe, r.Path);
    }

    [Fact]
    public void VersionsCompareNumericallyNotAsText()
    {
        ExtensionBinary("2.1.295");
        var newest = ExtensionBinary("2.1.1000");

        Assert.Equal(newest, ClaudeLocator.Find(null, null, Path.Combine(_root, "ext"), _ => true).Path);
    }

    [Fact]
    public void NothingWorksReturnsNullPathWithEverythingTried()
    {
        var configured = Touch("custom", "claude.exe");
        var cmd = Touch("npm", "claude.cmd");
        var ext = ExtensionBinary("2.1.295");

        var r = ClaudeLocator.Find(configured, Path.Combine(_root, "npm"), Path.Combine(_root, "ext"), _ => false);

        Assert.Null(r.Path);
        Assert.Equal([configured, cmd, ext], r.Tried);
    }

    [Fact]
    public void MissingExtensionsFolderIsFine() =>
        Assert.Null(ClaudeLocator.Find(null, null, Path.Combine(_root, "nope"), _ => true).Path);
}
