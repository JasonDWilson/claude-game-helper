namespace ClaudeGameHelper.Core.Claude;

public sealed record ClaudeRequest(
    string SessionId,
    bool IsFirstTurn,
    string Model,
    string Effort,
    string InstructionsPath,
    IReadOnlyList<string> ReadOnlyDirs);

public static class ClaudeArgsBuilder
{
    private const string Tools = "WebSearch,WebFetch,Read,Glob,Grep,Edit,Write";
    private const string AllowedTools = "WebSearch,WebFetch,Read,Glob,Grep,Edit(./tracking/**),Write(./tracking/**)";

    /// <summary>
    /// Arguments for one print-mode turn. <c>--safe-mode</c> keeps the user's own plugins, hooks and CLAUDE.md
    /// files out; <c>--restricted</c> confines file tools to the working dir and <c>--add-dir</c> dirs; writes
    /// are allowed only under <c>tracking/</c>. The prompt itself goes on stdin.
    /// </summary>
    public static IReadOnlyList<string> Build(ClaudeRequest r)
    {
        var args = new List<string>
        {
            "-p", "--output-format", "stream-json", "--verbose", "--include-partial-messages",
            "--model", r.Model, "--effort", r.Effort,
            r.IsFirstTurn ? "--session-id" : "--resume", r.SessionId,
            "--safe-mode", "--restricted", "--disable-slash-commands",
            "--tools", Tools,
            "--permission-mode", "dontAsk",
            "--allowedTools", AllowedTools,
        };

        foreach (var dir in r.ReadOnlyDirs)
        {
            args.Add("--add-dir");
            args.Add(dir);
        }

        args.Add("--append-system-prompt-file");
        args.Add(r.InstructionsPath);
        return args;
    }
}
