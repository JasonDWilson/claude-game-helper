using System.Diagnostics;
using System.Text;
using ClaudeGameHelper.Core.Claude;

namespace ClaudeGameHelper.Tests.Claude;

public class ClaudeRunnerTests
{
    [Fact]
    public void StartInfoUsesArgumentListAndUtf8WithoutBom()
    {
        var si = ClaudeRunner.CreateStartInfo(new("claude.exe", ["-p", "a \"quoted\" arg"], @"C:\p", "Grognak's Axe — worth it?\nline 2"));

        Assert.Equal(new[] { "-p", "a \"quoted\" arg" }, si.ArgumentList);
        Assert.Equal("", si.Arguments);
        Assert.Empty(si.StandardInputEncoding!.GetPreamble());
        Assert.Equal(Encoding.UTF8.WebName, si.StandardInputEncoding.WebName);
        Assert.Equal(Encoding.UTF8.WebName, si.StandardOutputEncoding!.WebName);
        Assert.True(si.RedirectStandardInput && si.RedirectStandardOutput && si.RedirectStandardError && si.CreateNoWindow);
        Assert.False(si.UseShellExecute);
        Assert.Equal(@"C:\p", si.WorkingDirectory);
    }

    [Fact]
    public async Task StreamsFixtureEventsAndCompletion()
    {
        var events = new List<ClaudeEvent>();

        var r = await new ClaudeRunner().RunAsync(
            new("cmd.exe", ["/c", "type", TestPaths.Fixture("resume-text-only.jsonl")], Path.GetTempPath(), ""),
            events.Add, CancellationToken.None);

        Assert.Equal(0, r.ExitCode);
        Assert.False(r.Canceled);
        Assert.NotNull(r.Completion);
        Assert.EndsWith("PINEAPPLE", r.Completion.ResultText);
        Assert.IsType<SessionStarted>(events[0]);
        Assert.IsType<Completed>(events[^1]);
    }

    [Fact]
    public async Task StdinReachesTheProcess()
    {
        var events = new List<ClaudeEvent>();
        var line = """{"type":"result","subtype":"success","is_error":false,"result":"echoed"}""";

        // findstr "^" copies stdin to stdout, so a JSON line written to stdin comes back as an event.
        var r = await new ClaudeRunner().RunAsync(new("findstr.exe", ["^"], Path.GetTempPath(), line), events.Add, CancellationToken.None);

        Assert.Equal("echoed", r.Completion?.ResultText);
    }

    [Fact]
    public async Task CancelKillsTheProcessQuickly()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var sw = Stopwatch.StartNew();

        var r = await new ClaudeRunner().RunAsync(
            new("powershell.exe", ["-NoProfile", "-Command", "Start-Sleep -Seconds 30"], Path.GetTempPath(), ""),
            _ => { }, cts.Token);

        Assert.True(r.Canceled);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"took {sw.Elapsed}");
    }

    [Fact]
    public async Task ProcessThatExitsWithoutReadingStdinIsNotAnException()
    {
        var r = await new ClaudeRunner().RunAsync(
            new("cmd.exe", ["/c", "exit /b 2"], Path.GetTempPath(), new string('x', 1_000_000)),
            _ => { }, CancellationToken.None);

        Assert.Equal(2, r.ExitCode);
    }

    [Fact]
    public async Task NonZeroExitReportsStderr()
    {
        var r = await new ClaudeRunner().RunAsync(
            new("cmd.exe", ["/c", "echo boom 1>&2 & exit /b 3"], Path.GetTempPath(), ""),
            _ => { }, CancellationToken.None);

        Assert.Equal(3, r.ExitCode);
        Assert.Contains("boom", r.Stderr);
        Assert.Null(r.Completion);
        Assert.False(r.Canceled);
    }
}
