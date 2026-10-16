using ClaudeGameHelper.Core.Claude;

namespace ClaudeGameHelper.Tests.Claude;

public class ClaudeArgsBuilderTests
{
    private const string Id = "11111111-1111-1111-1111-111111111111";

    [Fact]
    public void FirstTurnArgsAreExact()
    {
        var a = ClaudeArgsBuilder.Build(new(Id, true, "sonnet", "low", @"C:\p\CLAUDE.md", [@"C:\docs\Fallout 76"]));

        Assert.Equal(new[]
        {
            "-p", "--output-format", "stream-json", "--verbose", "--include-partial-messages",
            "--model", "sonnet", "--effort", "low", "--session-id", Id,
            "--safe-mode", "--restricted", "--disable-slash-commands",
            "--tools", "WebSearch,WebFetch,Read,Glob,Grep,Edit,Write",
            "--permission-mode", "dontAsk",
            "--allowedTools", "WebSearch,WebFetch,Read,Glob,Grep,Edit(./tracking/**),Write(./tracking/**)",
            "--add-dir", @"C:\docs\Fallout 76",
            "--append-system-prompt-file", @"C:\p\CLAUDE.md",
        }, a);
    }

    [Fact]
    public void LaterTurnsResume()
    {
        var a = ClaudeArgsBuilder.Build(new(Id, false, "opus", "medium", @"C:\p\CLAUDE.md", []));

        Assert.Equal(Id, a[a.ToList().IndexOf("--resume") + 1]);
        Assert.DoesNotContain("--session-id", a);
    }

    [Fact]
    public void NoReadOnlyDirsMeansNoAddDir() =>
        Assert.DoesNotContain("--add-dir", ClaudeArgsBuilder.Build(new(Id, true, "sonnet", "low", @"C:\p\CLAUDE.md", [])));

    [Fact]
    public void EachReadOnlyDirGetsItsOwnFlag()
    {
        var a = ClaudeArgsBuilder.Build(new(Id, true, "sonnet", "low", @"C:\p\CLAUDE.md", [@"C:\a", @"C:\b"]));

        Assert.Equal(2, a.Count(x => x == "--add-dir"));
    }
}
