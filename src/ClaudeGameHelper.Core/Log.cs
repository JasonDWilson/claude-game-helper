namespace ClaudeGameHelper.Core;

/// <summary>Appends lines to a daily log file. Does nothing until <see cref="Init"/> is called.</summary>
public static class Log
{
    private static readonly object Gate = new();
    private static string? _dir;

    public static void Init(string dir)
    {
        Directory.CreateDirectory(dir);
        _dir = dir;
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex == null ? message : $"{message}{Environment.NewLine}{ex}");

    private static void Write(string level, string message)
    {
        if (_dir == null)
            return;

        var now = DateTime.Now;
        var line = $"{now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}";
        lock (Gate)
        {
            try
            {
                File.AppendAllText(Path.Combine(_dir, $"helper-{now:yyyyMMdd}.log"), line);
            }
            catch (IOException)
            {
                // Logging must never take the app down.
            }
        }
    }
}
