using System.Windows.Interop;
using ClaudeGameHelper.Core.Hotkeys;

namespace ClaudeGameHelper.Platform;

public enum HotkeyAction
{
    Screen = 1,
    Ask = 2,
    Toggle = 3,
}

/// <summary>System-wide hotkeys, received on a message-only window on the creating (UI) thread.</summary>
public sealed class GlobalHotkeys : IDisposable
{
    private static readonly IntPtr HwndMessage = new(-3);
    private readonly HwndSource _source;
    private readonly List<int> _registered = [];

    public GlobalHotkeys()
    {
        _source = new HwndSource(new HwndSourceParameters("ClaudeGameHelperHotkeys") { ParentWindow = HwndMessage, WindowStyle = 0 });
        _source.AddHook(WndProc);
    }

    public event Action<HotkeyAction>? Pressed;

    /// <summary>Replaces the registered hotkeys with this set. Returns the chords Windows refused (already in use).</summary>
    public IReadOnlyList<string> Apply(HotkeyBindingSet set)
    {
        UnregisterAll();

        var failed = new List<string>();
        foreach (var (action, chord) in new[] { (HotkeyAction.Screen, set.Screen), (HotkeyAction.Ask, set.Ask), (HotkeyAction.Toggle, set.Toggle) })
        {
            if (NativeMethods.RegisterHotKey(_source.Handle, (int)action, (uint)chord.Modifiers | NativeMethods.ModNoRepeat, chord.VirtualKey))
                _registered.Add((int)action);
            else
                failed.Add(chord.Text);
        }
        return failed;
    }

    public void Dispose()
    {
        if (_source.IsDisposed)
            return;
        UnregisterAll();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private void UnregisterAll()
    {
        foreach (var id in _registered)
            NativeMethods.UnregisterHotKey(_source.Handle, id);
        _registered.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WmHotkey && Enum.IsDefined(typeof(HotkeyAction), wParam.ToInt32()))
        {
            handled = true;
            Pressed?.Invoke((HotkeyAction)wParam.ToInt32());
        }
        return IntPtr.Zero;
    }
}
