using System.Windows;
using System.Windows.Interop;

namespace ClaudeGameHelper.Platform;

public static class FocusHelper
{
    /// <summary>Gives keyboard focus back to a window (the game), if it still exists.</summary>
    public static void ReturnTo(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero && NativeMethods.IsWindow(hwnd))
            NativeMethods.SetForegroundWindow(hwnd);
    }

    /// <summary>Turns WS_EX_NOACTIVATE on or off, so clicks and showing the window do or don't take focus.</summary>
    public static void SetNoActivate(Window window, bool on)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle).ToInt64();
        style = on ? style | NativeMethods.WsExNoActivate : style & ~NativeMethods.WsExNoActivate;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlExStyle, new IntPtr(style));
    }
}
