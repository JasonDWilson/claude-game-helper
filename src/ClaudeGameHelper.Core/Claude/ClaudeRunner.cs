using System.Diagnostics;
using System.Text;

namespace ClaudeGameHelper.Core.Claude;

public sealed record ClaudeLaunch(string ExePath, IReadOnlyList<string> Args, string WorkingDirectory, string StdinText);

public sealed record ClaudeRunResult(int? ExitCode, bool Canceled, string Stderr, Completed? Completion);

public interface IClaudeRunner
{
    Task<ClaudeRunResult> RunAsync(ClaudeLaunch launch, Action<ClaudeEvent> onEvent, CancellationToken ct);
}

/// <summary>Runs Claude Code once: prompt on stdin, events parsed from stdout as they arrive.</summary>
public sealed class ClaudeRunner : IClaudeRunner
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static ProcessStartInfo CreateStartInfo(ClaudeLaunch launch)
    {
        var si = new ProcessStartInfo(launch.ExePath)
        {
            WorkingDirectory = launch.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Utf8NoBom,
            StandardOutputEncoding = Utf8NoBom,
            StandardErrorEncoding = Utf8NoBom,
        };
        foreach (var arg in launch.Args)
            si.ArgumentList.Add(arg);
        return si;
    }

    public async Task<ClaudeRunResult> RunAsync(ClaudeLaunch launch, Action<ClaudeEvent> onEvent, CancellationToken ct)
    {
        Log.Info($"Running {launch.ExePath} {string.Join(' ', launch.Args)} in {launch.WorkingDirectory}");

        using var process = Process.Start(CreateStartInfo(launch))
            ?? throw new InvalidOperationException($"Couldn't start {launch.ExePath}.");

        Completed? completion = null;
        var stdout = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync() is { } line)
            {
                foreach (var e in StreamJsonParser.ParseLine(line))
                {
                    if (e is Completed c)
                        completion = c;
                    onEvent(e);
                }
            }
        }, CancellationToken.None);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);

        try
        {
            await process.StandardInput.WriteAsync(launch.StdinText);
            process.StandardInput.Close();
        }
        catch (IOException ex)
        {
            // The process exited without reading its input; its exit code and stderr say why.
            Log.Info($"Claude Code closed stdin early: {ex.Message}");
        }

        var canceled = false;
        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
        }

        await stdout;
        var errorText = await stderr;
        int? exitCode = canceled ? null : process.ExitCode;
        Log.Info(canceled ? "Claude Code run canceled." : $"Claude Code exited with {exitCode}.");
        if (!string.IsNullOrWhiteSpace(errorText))
            Log.Info($"Claude Code stderr: {errorText.Trim()}");

        return new ClaudeRunResult(exitCode, canceled, errorText, completion);
    }
}
