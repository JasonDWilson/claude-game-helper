using System.Text.Json;

namespace ClaudeGameHelper.Core.Claude;

/// <summary>Turns one line of <c>claude -p --output-format stream-json</c> output into events.</summary>
public static class StreamJsonParser
{
    public static IReadOnlyList<ClaudeEvent> ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return [];

            return Str(root, "type") switch
            {
                "system" => System(root),
                "stream_event" => StreamEvent(root),
                "assistant" => Assistant(root),
                "rate_limit_event" => RateLimit(root),
                "result" => [new Completed(Str(root, "result") ?? "", Bool(root, "is_error"), Str(root, "subtype"))],
                _ => [],
            };
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<ClaudeEvent> System(JsonElement root) => Str(root, "subtype") switch
    {
        "init" => [new SessionStarted(Str(root, "session_id") ?? "", Str(root, "model") ?? "")],
        "permission_denied" => [new ToolDenied(Str(root, "tool_name") ?? "", Str(root, "message") ?? "")],
        _ => [],
    };

    private static IReadOnlyList<ClaudeEvent> StreamEvent(JsonElement root)
    {
        if (!root.TryGetProperty("event", out var ev))
            return [];

        switch (Str(ev, "type"))
        {
            case "content_block_delta" when ev.TryGetProperty("delta", out var delta) && Str(delta, "type") == "text_delta":
                return [new TextDelta(Str(delta, "text") ?? "")];
            case "content_block_start" when ev.TryGetProperty("content_block", out var block) && Str(block, "type") == "thinking":
                return [new Activity("Thinking…")];
            default:
                return [];
        }
    }

    private static IReadOnlyList<ClaudeEvent> Assistant(JsonElement root)
    {
        if (!root.TryGetProperty("message", out var message))
            return [];

        var events = new List<ClaudeEvent>();
        if (message.TryGetProperty("usage", out var usage))
            events.Add(new ContextSize(Int(usage, "input_tokens") + Int(usage, "cache_read_input_tokens") + Int(usage, "cache_creation_input_tokens")));

        if (message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                if (Str(block, "type") == "tool_use")
                    events.Add(new Activity(Describe(Str(block, "name") ?? "", block.TryGetProperty("input", out var input) ? input : default)));
            }
        }

        return events;
    }

    private static IReadOnlyList<ClaudeEvent> RateLimit(JsonElement root)
    {
        if (!root.TryGetProperty("rate_limit_info", out var info) || Str(info, "status") != "rejected")
            return [];

        DateTimeOffset? resetsAt = info.TryGetProperty("resetsAt", out var r) && r.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
        return [new UsageLimit(resetsAt)];
    }

    private static string Describe(string tool, JsonElement input) => tool switch
    {
        "WebSearch" => $"Searching the web: {Str(input, "query")}",
        "WebFetch" => $"Reading {Host(Str(input, "url"))}",
        "Read" => $"Looking at {FileName(Str(input, "file_path"))}",
        "Edit" or "Write" => $"Updating {FileName(Str(input, "file_path"))}",
        "Glob" or "Grep" => "Checking files",
        _ => $"Using {tool}",
    };

    private static string Host(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url ?? "a web page";

    private static string FileName(string? path) =>
        string.IsNullOrEmpty(path) ? "a file" : Path.GetFileName(path.Replace('/', '\\'));

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

    private static int Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.TryGetInt32(out var n) ? n : 0;
}
