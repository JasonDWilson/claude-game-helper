using System.Text;

namespace ClaudeGameHelper.Platform;

public sealed record WindowInfo(IntPtr Hwnd, int ProcessId, string ProcessName, string Title)
{
    /// <summary>Describes a top-level window: its process name (without ".exe") and title. Null when there is no such window.</summary>
    public static WindowInfo? FromHandle(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
            return null;

        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        return new WindowInfo(hwnd, processId, ReadProcessName(processId), ReadTitle(hwnd));
    }

    private static string ReadProcessName(int processId)
    {
        var process = NativeMethods.OpenProcess(NativeMethods.ProcessQueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero)
            return "";
        try
        {
            var buffer = new StringBuilder(1024);
            var size = buffer.Capacity;
            return NativeMethods.QueryFullProcessImageName(process, 0, buffer, ref size)
                ? Path.GetFileNameWithoutExtension(buffer.ToString())
                : "";
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }
    }

    private static string ReadTitle(IntPtr hwnd)
    {
        var length = NativeMethods.GetWindowTextLength(hwnd);
        var buffer = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }
}
