using ClaudeGameHelper.Core.Hotkeys;
using ClaudeGameHelper.Platform;

namespace ClaudeGameHelper.Tests.Platform;

public class GlobalHotkeysTests : IDisposable
{
    private readonly StaWindowHost _host = new();

    public void Dispose() => _host.Dispose();

    private static readonly HotkeyBindingSet Rare = new(
        HotkeyChord.Parse("Ctrl+Alt+Shift+F22"),
        HotkeyChord.Parse("Ctrl+Alt+Shift+F23"),
        HotkeyChord.Parse("Ctrl+Alt+Shift+F24"));

    [Fact]
    public void RegistersAndReportsConflicts()
    {
        var a = _host.Invoke(() => new GlobalHotkeys());
        var b = _host.Invoke(() => new GlobalHotkeys());
        try
        {
            Assert.Empty(_host.Invoke(() => a.Apply(Rare)));
            Assert.Equal(["Ctrl+Alt+Shift+F22", "Ctrl+Alt+Shift+F23", "Ctrl+Alt+Shift+F24"], _host.Invoke(() => b.Apply(Rare)));

            _host.Invoke(a.Dispose);

            Assert.Empty(_host.Invoke(() => b.Apply(Rare)));
        }
        finally
        {
            _host.Invoke(a.Dispose);
            _host.Invoke(b.Dispose);
        }
    }

    [Fact]
    public void ApplyingAgainReplacesTheOldSet()
    {
        var a = _host.Invoke(() => new GlobalHotkeys());
        var b = _host.Invoke(() => new GlobalHotkeys());
        try
        {
            _host.Invoke(() => a.Apply(Rare));
            _host.Invoke(() => a.Apply(Rare with { Screen = HotkeyChord.Parse("Ctrl+Alt+Shift+F21") }));

            Assert.Equal(["Ctrl+Alt+Shift+F23", "Ctrl+Alt+Shift+F24"], _host.Invoke(() => b.Apply(Rare)));
        }
        finally
        {
            _host.Invoke(a.Dispose);
            _host.Invoke(b.Dispose);
        }
    }
}
