using System.Net.Http;
using ClaudeGameHelper.Platform;

namespace ClaudeGameHelper.Tests.Platform;

public class UpdateCheckerTests
{
    [Fact]
    public async Task SafelySwallowsAndLogsFailures() =>
        await UpdateChecker.Safely(() => throw new HttpRequestException("offline"));

    [Fact]
    public async Task SafelySwallowsAsyncFailures() =>
        await UpdateChecker.Safely(async () => { await Task.Yield(); throw new InvalidOperationException("rate limited"); });

    [Fact]
    public async Task RunAsyncIsANoOpWhenNotInstalled() => await UpdateChecker.RunAsync();

    [Fact]
    public void RepoUrlPointsAtTheGitHubRepo() =>
        Assert.Equal("https://github.com/JasonDWilson/claude-game-helper", UpdateChecker.RepoUrl);

    [Fact]
    public void VersionIsDevWhenNotInstalled() => Assert.Equal("dev", AppVersion.Current);
}
