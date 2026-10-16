using ClaudeGameHelper.Core.Conversations;

namespace ClaudeGameHelper.Tests.Conversations;

public class ConversationArchiveTests
{
    [Fact]
    public void ArchiveWritesHeaderOnceThenTurns()
    {
        var profileDir = TestPaths.NewTempDir();
        var archive = Path.Combine(profileDir, "conversations", "2026-10-16_201500.md");
        var shot = Path.Combine(profileDir, "screenshots", "a.jpg");

        ConversationArchive.AppendTurn(archive, "Fallout 76", new DateTime(2026, 10, 16, 20, 15, 0), "Is this good?", [shot], "sonnet", "Scrip it.");
        ConversationArchive.AppendTurn(archive, "Fallout 76", new DateTime(2026, 10, 16, 20, 16, 0), "And this?", [], "opus", "Keep it.");

        Assert.Equal(
            "# Fallout 76 — 2026-10-16 20:15\n\n" +
            "## You · 20:15\nIs this good?\n\n![screenshot](../screenshots/a.jpg)\n\n" +
            "## Claude · sonnet · 20:15\nScrip it.\n\n" +
            "## You · 20:16\nAnd this?\n\n" +
            "## Claude · opus · 20:16\nKeep it.\n\n",
            File.ReadAllText(archive));
    }
}
