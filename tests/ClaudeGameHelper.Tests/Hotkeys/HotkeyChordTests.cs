using ClaudeGameHelper.Core.Hotkeys;
using ClaudeGameHelper.Core.Profiles;
using ClaudeGameHelper.Core.Settings;

namespace ClaudeGameHelper.Tests.Hotkeys;

public class HotkeyChordTests
{
    [Theory]
    [InlineData("Ctrl+Shift+F1", HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, 0x70u)]
    [InlineData(" control + alt + k ", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, 0x4Bu)]
    [InlineData("Win+Shift+F24", HotkeyModifiers.Win | HotkeyModifiers.Shift, 0x87u)]
    [InlineData("Ctrl+7", HotkeyModifiers.Ctrl, 0x37u)]
    public void ParsesChords(string text, HotkeyModifiers mods, uint vk)
    {
        var c = HotkeyChord.Parse(text);

        Assert.Equal(mods, c.Modifiers);
        Assert.Equal(vk, c.VirtualKey);
        Assert.Equal(text, c.Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Banana")]
    [InlineData("Ctrl+F1+F2")]
    [InlineData("Ctrl+F25")]
    public void RejectsBadChords(string text) =>
        Assert.Contains($"\"{text}\"", Assert.Throws<FormatException>(() => HotkeyChord.Parse(text)).Message);

    [Fact]
    public void DefaultsWithoutProfile()
    {
        var set = HotkeyBindings.Resolve(new AppSettings(), null);

        Assert.Equal("Ctrl+Shift+F1", set.Screen.Text);
        Assert.Equal("Ctrl+Shift+F2", set.Ask.Text);
        Assert.Equal("Ctrl+Shift+F3", set.Toggle.Text);
    }

    [Fact]
    public void ProfileOverrideWinsPerKey()
    {
        var profile = new GameProfile("g", "G", @"C:\g", null, [], null, new HotkeyOverrides("Ctrl+Alt+S", null, null), null, false, false);

        var set = HotkeyBindings.Resolve(new AppSettings(), profile);

        Assert.Equal("Ctrl+Alt+S", set.Screen.Text);
        Assert.Equal("Ctrl+Shift+F2", set.Ask.Text);
        Assert.Equal("Ctrl+Shift+F3", set.Toggle.Text);
    }
}
