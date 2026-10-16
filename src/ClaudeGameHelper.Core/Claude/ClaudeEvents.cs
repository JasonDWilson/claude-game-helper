namespace ClaudeGameHelper.Core.Claude;

/// <summary>Something that happened while Claude Code answered, parsed from its stream-json output.</summary>
public abstract record ClaudeEvent;

public sealed record SessionStarted(string SessionId, string Model) : ClaudeEvent;

public sealed record TextDelta(string Text) : ClaudeEvent;

/// <summary>What Claude is doing right now, as text for the panel's status line.</summary>
public sealed record Activity(string Text) : ClaudeEvent;

/// <summary>The conversation's size: input + cache-read + cache-creation tokens of the latest message.</summary>
public sealed record ContextSize(int Tokens) : ClaudeEvent;

public sealed record ToolDenied(string ToolName, string Message) : ClaudeEvent;

public sealed record UsageLimit(DateTimeOffset? ResetsAt) : ClaudeEvent;

public sealed record Completed(string ResultText, bool IsError, string? Subtype) : ClaudeEvent;
