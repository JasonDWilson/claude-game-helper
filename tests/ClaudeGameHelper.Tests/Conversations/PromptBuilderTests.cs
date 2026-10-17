using ClaudeGameHelper.Core.Conversations;

namespace ClaudeGameHelper.Tests.Conversations;

public class PromptBuilderTests
{
    [Fact]
    public void ScreenQuestionText() =>
        Assert.Equal("What am I looking at, and what should I do?", PromptBuilder.ScreenQuestion);

    [Fact]
    public void PromptWithScreenshotsAndActiveFile() =>
        Assert.Equal(
            "Is this good?\n\nScreenshots of my game screen (view each with the Read tool):\n- C:\\p\\screenshots\\a.jpg\n- C:\\p\\screenshots\\b.jpg\n\nActive character file: tracking/vault-dweller.md",
            PromptBuilder.Build("Is this good?", [@"C:\p\screenshots\a.jpg", @"C:\p\screenshots\b.jpg"], null, "Character", "vault-dweller"));

    [Fact]
    public void PromptWithNoActiveFileSaysSo() =>
        Assert.EndsWith("\n\nNo character file is selected yet.", PromptBuilder.Build("q", [], null, "Character", null));

    [Fact]
    public void PromptWithoutTrackingLabelIsJustTheQuestion() =>
        Assert.Equal("q", PromptBuilder.Build("q", [], null, null, null));

    [Fact]
    public void QuestionIsTrimmed() =>
        Assert.Equal("q", PromptBuilder.Build("  q \n", [], null, null, null));

    [Fact]
    public void PromptNamesTheGame() =>
        Assert.Equal("q\n\nGame: Valheim\n\nNo notes file is selected yet.", PromptBuilder.Build("q", [], "Valheim", "Notes", null));

    [Fact]
    public void PromptCarriesRecentWatchMessages() =>
        Assert.EndsWith(
            "\n\nWatch mode recently told me:\n- Legendary enemy to the left.",
            PromptBuilder.Build("q", [], null, null, null, ["Legendary enemy to the left."]));

    [Fact]
    public void NoRecentWatchMessagesAddsNothing() =>
        Assert.Equal("q", PromptBuilder.Build("q", [], null, null, null, []));

    [Fact]
    public void GameLineComesAfterScreenshots() =>
        Assert.Equal(
            "q\n\nScreenshots of my game screen (view each with the Read tool):\n- a.jpg\n\nGame: Valheim",
            PromptBuilder.Build("q", ["a.jpg"], "Valheim", null, null));
}
