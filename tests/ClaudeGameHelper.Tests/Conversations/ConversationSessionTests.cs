using ClaudeGameHelper.Core.Conversations;
using ClaudeGameHelper.Core.Profiles;

namespace ClaudeGameHelper.Tests.Conversations;

public class ConversationSessionTests
{
    private readonly GameProfile _profile = new("g", "G", TestPaths.NewTempDir(), null, [], null, null, null, false, false);
    private DateTime _now = new(2026, 10, 16, 20, 15, 0);

    private ConversationSession New(int rollover = 100_000) =>
        new(_profile, rollover, Guid.NewGuid, () => _now);

    [Fact]
    public void FirstTurnUsesNewSessionThenResumes()
    {
        var s = New();

        Assert.False(s.BeginTurn());
        Assert.False(s.HasStarted);
        Assert.True(Guid.TryParse(s.SessionId, out _));

        s.MarkStarted();
        Assert.True(s.HasStarted);
        var id = s.SessionId;
        Assert.False(s.BeginTurn());
        Assert.Equal(id, s.SessionId);
    }

    [Fact]
    public void RollsOverAtThreshold()
    {
        var s = New(rollover: 100_000);
        var first = s.SessionId;
        s.MarkStarted();
        s.RecordContext(99_999);
        Assert.False(s.BeginTurn());

        s.RecordContext(100_000);
        _now = _now.AddMinutes(30);

        Assert.True(s.BeginTurn());
        Assert.NotEqual(first, s.SessionId);
        Assert.False(s.HasStarted);
        Assert.Equal(Path.Combine(_profile.ConversationsDir, "2026-10-16_204500.md"), s.ArchivePath);
        Assert.False(s.BeginTurn());
    }

    [Fact]
    public void ArchiveFileNamedForSessionStart() =>
        Assert.Equal(Path.Combine(_profile.ConversationsDir, "2026-10-16_201500.md"), New().ArchivePath);

    [Fact]
    public void ResetStartsFreshSessionAndArchive()
    {
        var s = New();
        var first = s.SessionId;
        s.MarkStarted();
        _now = _now.AddMinutes(1);

        s.Reset();

        Assert.NotEqual(first, s.SessionId);
        Assert.False(s.HasStarted);
        Assert.Equal(Path.Combine(_profile.ConversationsDir, "2026-10-16_201600.md"), s.ArchivePath);
    }
}
