using ClaudeGameHelper.Core.Claude;
using ClaudeGameHelper.Core.Profiles;
using ClaudeGameHelper.Core.Settings;

namespace ClaudeGameHelper.Core.Conversations;

public sealed record AskRequest(GameProfile Profile, string Question, IReadOnlyList<string> ScreenshotPaths, bool Deep, string? ActiveTrackingName);

public sealed record AskOutcome(string AnswerText, bool IsError, bool Canceled, bool RolledOver, string Model);

public interface IAskService
{
    bool IsBusy { get; }
    Task<AskOutcome> AskAsync(AskRequest request, Action<ClaudeEvent> onEvent, CancellationToken ct);
    void NewConversation(GameProfile profile);
}

/// <summary>
/// Sends one question (with any screenshots) to Claude Code in the profile's conversation and returns the
/// answer. UI-free, so a timer can call it as well as a hotkey.
/// </summary>
public sealed class AskService(AppSettings settings, Func<ClaudeLocateResult> locateClaude, IClaudeRunner runner, Func<DateTime> now)
    : IAskService
{
    public const string BusyMessage = "Still answering — wait for it or press Stop.";

    private readonly Dictionary<string, ConversationSession> _sessions = [];
    private string? _claudePath;
    private int _busy;

    public bool IsBusy => Volatile.Read(ref _busy) == 1;

    public async Task<AskOutcome> AskAsync(AskRequest request, Action<ClaudeEvent> onEvent, CancellationToken ct)
    {
        var profile = request.Profile;
        var model = ModelSelector.Choose(settings, profile, request.ScreenshotPaths.Count, request.Deep);

        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            return new AskOutcome(BusyMessage, true, false, false, model.Model);

        try
        {
            if (_claudePath == null)
            {
                // Probing runs "claude --version"; keep that off the caller's (UI) thread.
                var located = await Task.Run(locateClaude, ct);
                if (located.Path == null)
                {
                    return new AskOutcome(
                        $"Claude Code wasn't found. Tried: {string.Join(", ", located.Tried)}. Set \"claudePath\" in settings.json to a working claude.exe.",
                        true, false, false, model.Model);
                }
                _claudePath = located.Path;
            }

            var session = Session(profile);
            var rolledOver = session.BeginTurn();
            // "New conversation" can reset the session mid-answer; this run's events then belong to the old one.
            var sessionId = session.SessionId;
            var archivePath = session.ArchivePath;
            var args = ClaudeArgsBuilder.Build(new ClaudeRequest(
                session.SessionId, !session.HasStarted, model.Model, model.Effort, profile.InstructionsPath,
                profile.ReadOnlyPaths.Where(Directory.Exists).ToList()));
            var prompt = PromptBuilder.Build(request.Question, request.ScreenshotPaths,
                profile.IsFallback ? null : profile.DisplayName, profile.TrackingLabel, request.ActiveTrackingName);
            // Claude runs inside the game's data folder; a new game (or a deleted folder) doesn't have one yet.
            Directory.CreateDirectory(profile.FolderPath);

            ClaudeRunResult result;
            try
            {
                result = await runner.RunAsync(new ClaudeLaunch(_claudePath, args, profile.FolderPath, prompt), e =>
                {
                    if (session.SessionId == sessionId)
                    {
                        if (e is SessionStarted)
                            session.MarkStarted();
                        else if (e is ContextSize size)
                            session.RecordContext(size.Tokens);
                    }
                    onEvent(e);
                }, ct);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
            {
                Log.Error($"Couldn't start Claude Code at {_claudePath}.", ex);
                var failedPath = _claudePath;
                _claudePath = null;
                return new AskOutcome($"Couldn't start Claude Code ({failedPath}): {ex.Message}", true, false, rolledOver, model.Model);
            }

            var outcome = ToOutcome(result, rolledOver, model.Model);
            Archive(archivePath, profile, request, model.Model, outcome.AnswerText);
            return outcome;
        }
        finally
        {
            Volatile.Write(ref _busy, 0);
        }
    }

    public void NewConversation(GameProfile profile) => Session(profile).Reset();

    private ConversationSession Session(GameProfile profile)
    {
        if (!_sessions.TryGetValue(profile.Id, out var session))
        {
            session = new ConversationSession(profile, settings.RolloverContextTokens, Guid.NewGuid, now);
            _sessions[profile.Id] = session;
        }
        return session;
    }

    private static AskOutcome ToOutcome(ClaudeRunResult result, bool rolledOver, string model)
    {
        if (result.Canceled)
            return new AskOutcome("Stopped.", false, true, rolledOver, model);

        if (result.Completion is { } done)
        {
            var text = done.IsError && string.IsNullOrWhiteSpace(done.ResultText) ? "Claude couldn't answer (no details)." : done.ResultText;
            return new AskOutcome(text, done.IsError, false, rolledOver, model);
        }

        var firstLine = result.Stderr.Trim().Split('\n')[0].Trim();
        if (firstLine.Length > 300)
            firstLine = firstLine[..300];
        return new AskOutcome($"Claude Code stopped unexpectedly (exit {result.ExitCode}): {firstLine}", true, false, rolledOver, model);
    }

    private void Archive(string archivePath, GameProfile profile, AskRequest request, string model, string answer)
    {
        try
        {
            ConversationArchive.AppendTurn(archivePath, profile.DisplayName, now(), request.Question, request.ScreenshotPaths, model, answer);
        }
        catch (IOException ex)
        {
            Log.Error("Couldn't write the conversation archive.", ex);
        }
    }
}
