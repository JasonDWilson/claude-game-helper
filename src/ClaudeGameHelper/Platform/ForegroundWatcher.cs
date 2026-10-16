namespace ClaudeGameHelper.Platform;

/// <summary>Reports each time a different window comes to the foreground. Create it on the UI thread.</summary>
public sealed class ForegroundWatcher : IDisposable
{
    // Held in a field so the garbage collector doesn't free the delegate Windows calls back into.
    private readonly NativeMethods.WinEventProc _proc;
    private IntPtr _hook;

    public ForegroundWatcher()
    {
        _proc = OnWinEvent;
        _hook = NativeMethods.SetWinEventHook(NativeMethods.EventSystemForeground, NativeMethods.EventSystemForeground,
            IntPtr.Zero, _proc, 0, 0, NativeMethods.WinEventOutOfContext);
    }

    public event Action<WindowInfo>? Changed;

    public static WindowInfo? Current() => WindowInfo.FromHandle(NativeMethods.GetForegroundWindow());

    public void Dispose()
    {
        if (_hook == IntPtr.Zero)
            return;
        NativeMethods.UnhookWinEvent(_hook);
        _hook = IntPtr.Zero;
    }

    private void OnWinEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (WindowInfo.FromHandle(hwnd) is { } info)
            Changed?.Invoke(info);
    }
}
