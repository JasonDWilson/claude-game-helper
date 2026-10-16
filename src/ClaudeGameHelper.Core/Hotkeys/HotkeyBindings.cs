using ClaudeGameHelper.Core.Profiles;
using ClaudeGameHelper.Core.Settings;

namespace ClaudeGameHelper.Core.Hotkeys;

public sealed record HotkeyBindingSet(HotkeyChord Screen, HotkeyChord Ask, HotkeyChord Toggle);

public static class HotkeyBindings
{
    /// <summary>The settings' hotkeys, with any the profile overrides replaced.</summary>
    public static HotkeyBindingSet Resolve(AppSettings settings, GameProfile? profile) => new(
        HotkeyChord.Parse(profile?.Hotkeys?.Screen ?? settings.ScreenHotkey),
        HotkeyChord.Parse(profile?.Hotkeys?.Ask ?? settings.AskHotkey),
        HotkeyChord.Parse(profile?.Hotkeys?.Toggle ?? settings.ToggleHotkey));
}
