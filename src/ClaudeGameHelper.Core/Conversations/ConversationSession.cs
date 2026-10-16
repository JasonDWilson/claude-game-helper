using ClaudeGameHelper.Core.Profiles;

namespace ClaudeGameHelper.Core.Conversations;

/// <summary>
/// One Claude Code conversation for a profile: its session id, whether Claude has started it (so later turns
/// resume it), its size, and its archive file. Rolls over to a fresh conversation once it gets large.
/// </summary>
public sealed class ConversationSession
{
    private readonly GameProfile _profile;
    private readonly int _rolloverTokens;
    private readonly Func<Guid> _newId;
    private readonly Func<DateTime> _now;
    private int _contextTokens;

    public ConversationSession(GameProfile profile, int rolloverTokens, Func<Guid> newId, Func<DateTime> now)
    {
        _profile = profile;
        _rolloverTokens = rolloverTokens;
        _newId = newId;
        _now = now;
        Reset();
    }

    public string SessionId { get; private set; } = "";
    public bool HasStarted { get; private set; }
    public string ArchivePath { get; private set; } = "";

    /// <summary>Call before each turn. Returns true when the conversation rolled over to a fresh one first.</summary>
    public bool BeginTurn()
    {
        if (_contextTokens < _rolloverTokens)
            return false;

        Reset();
        return true;
    }

    public void MarkStarted() => HasStarted = true;

    public void RecordContext(int tokens) => _contextTokens = tokens;

    public void Reset()
    {
        SessionId = _newId().ToString();
        HasStarted = false;
        _contextTokens = 0;
        ArchivePath = Path.Combine(_profile.ConversationsDir, $"{_now():yyyy-MM-dd_HHmmss}.md");
    }
}
