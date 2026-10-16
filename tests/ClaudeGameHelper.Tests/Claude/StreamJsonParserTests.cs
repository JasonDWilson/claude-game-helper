using ClaudeGameHelper.Core.Claude;

namespace ClaudeGameHelper.Tests.Claude;

public class StreamJsonParserTests
{
    private static List<ClaudeEvent> ParseFixture(string name) =>
        File.ReadLines(TestPaths.Fixture(name)).SelectMany(StreamJsonParser.ParseLine).ToList();

    private static string AssistantToolUse(string tool, string inputJson) =>
        $$$"""{"type":"assistant","message":{"content":[{"type":"tool_use","id":"t1","name":"{{{tool}}}","input":{{{inputJson}}}}]}}""";

    [Fact]
    public void ToolsFixture()
    {
        var ev = ParseFixture("tools-and-denials.jsonl");

        Assert.Equal(new SessionStarted("97e4711d-90c5-4b5a-bc95-ed6aafca3d69", "claude-haiku-5-5"), ev[0]);
        Assert.Contains(new Activity("Updating a.md"), ev);
        Assert.Contains(new Activity("Looking at prefs.ini"), ev);
        Assert.Equal(3, ev.OfType<ToolDenied>().Count());
        var done = Assert.IsType<Completed>(ev[^1]);
        Assert.False(done.IsError);
        Assert.Equal("success", done.Subtype);
        Assert.EndsWith("PINEAPPLE", done.ResultText);
    }

    [Fact]
    public void ResumeFixture()
    {
        var ev = ParseFixture("resume-text-only.jsonl");

        Assert.Equal("claude-sonnet-5-5", ev.OfType<SessionStarted>().Single().Model);
        Assert.Equal(10959, ev.OfType<ContextSize>().Last().Tokens);
        Assert.Equal(
            "Step 4's answer was `iSize W=2560`, so the value is 2560.\n\nPINEAPPLE",
            string.Concat(ev.OfType<TextDelta>().Select(d => d.Text)));
    }

    [Theory]
    [InlineData("WebSearch", """{"query":"treasury notes farming"}""", "Searching the web: treasury notes farming")]
    [InlineData("WebFetch", """{"url":"https://fallout.fandom.com/wiki/Treasury_note","prompt":"x"}""", "Reading fallout.fandom.com")]
    [InlineData("Read", """{"file_path":"C:\\p\\screenshots\\20261016-201512-123.jpg"}""", "Looking at 20261016-201512-123.jpg")]
    [InlineData("Edit", """{"file_path":"C:\\p\\tracking\\vault-dweller.md"}""", "Updating vault-dweller.md")]
    [InlineData("Grep", """{"pattern":"x"}""", "Checking files")]
    [InlineData("Glob", """{"pattern":"x"}""", "Checking files")]
    [InlineData("Agent", """{}""", "Using Agent")]
    public void ToolUseBecomesActivity(string tool, string input, string expected) =>
        Assert.Contains(new Activity(expected), StreamJsonParser.ParseLine(AssistantToolUse(tool, input)));

    [Fact]
    public void ThinkingBlockStartIsThinkingActivity() =>
        Assert.Equal(
            [new Activity("Thinking…")],
            StreamJsonParser.ParseLine("""{"type":"stream_event","event":{"type":"content_block_start","index":0,"content_block":{"type":"thinking","thinking":""}}}"""));

    [Fact]
    public void RejectedRateLimitIsUsageLimit()
    {
        Assert.Equal(
            [new UsageLimit(DateTimeOffset.FromUnixTimeSeconds(1792189800))],
            StreamJsonParser.ParseLine("""{"type":"rate_limit_event","rate_limit_info":{"status":"rejected","resetsAt":1792189800}}"""));
        Assert.Empty(StreamJsonParser.ParseLine("""{"type":"rate_limit_event","rate_limit_info":{"status":"allowed_warning","resetsAt":1792189800}}"""));
    }

    [Fact]
    public void ErrorResultIsCompletedWithError() =>
        Assert.Equal(
            [new Completed("You've hit your limit · resets 3pm", true, "success")],
            StreamJsonParser.ParseLine("""{"type":"result","subtype":"success","is_error":true,"result":"You've hit your limit · resets 3pm"}"""));

    [Fact]
    public void ResultWithoutTextIsEmptyString() =>
        Assert.Equal(
            [new Completed("", true, "error_during_execution")],
            StreamJsonParser.ParseLine("""{"type":"result","subtype":"error_during_execution","is_error":true}"""));

    [Fact]
    public void GarbageAndUnknownLinesYieldNothing()
    {
        Assert.Empty(StreamJsonParser.ParseLine("not json"));
        Assert.Empty(StreamJsonParser.ParseLine(""));
        Assert.Empty(StreamJsonParser.ParseLine("""{"type":"system","subtype":"status"}"""));
        Assert.Empty(StreamJsonParser.ParseLine("""[1,2]"""));
    }
}
