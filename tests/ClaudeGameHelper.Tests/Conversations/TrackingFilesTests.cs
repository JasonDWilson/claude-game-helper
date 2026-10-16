using ClaudeGameHelper.Core.Conversations;
using ClaudeGameHelper.Core.Profiles;

namespace ClaudeGameHelper.Tests.Conversations;

public class TrackingFilesTests
{
    private readonly GameProfile _profile = new("g", "G", TestPaths.NewTempDir(), null, [], "Character", null, null, false, false);

    [Fact]
    public void TrackingListsMarkdownNamesOnlySorted()
    {
        Directory.CreateDirectory(_profile.TrackingDir);
        foreach (var name in new[] { "b.md", "A.md", "notes.txt", ".gitkeep" })
            File.WriteAllText(Path.Combine(_profile.TrackingDir, name), "");

        Assert.Equal(["A", "b"], TrackingFiles.List(_profile));
    }

    [Fact]
    public void MissingTrackingDirIsEmpty() => Assert.Empty(TrackingFiles.List(_profile));

    [Fact]
    public void Paths()
    {
        Assert.Equal("tracking/vault-dweller.md", TrackingFiles.RelativePath("vault-dweller"));
        Assert.Equal(Path.Combine(_profile.TrackingDir, "vault-dweller.md"), TrackingFiles.FullPath(_profile, "vault-dweller"));
    }
}
