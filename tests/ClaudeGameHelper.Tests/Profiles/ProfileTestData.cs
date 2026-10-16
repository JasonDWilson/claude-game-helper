using ClaudeGameHelper.Core.Profiles;

namespace ClaudeGameHelper.Tests.Profiles;

internal static class ProfileTestData
{
    public static GameProfile Profile(string id, string[]? processNames = null, string[]? titles = null) =>
        new(id, id, Path.Combine(Path.GetTempPath(), id),
            processNames == null && titles == null ? null : new WindowMatch(processNames ?? [], titles ?? []),
            [], null, null, null, false, false);

    public static void WriteProfile(string root, string id, string json, bool withClaudeMd = true)
    {
        var dir = Path.Combine(root, id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "profile.json"), json);
        if (withClaudeMd)
            File.WriteAllText(Path.Combine(dir, "CLAUDE.md"), "# test");
    }
}
