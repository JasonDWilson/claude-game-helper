namespace ClaudeGameHelper.Core.Hotkeys;

/// <summary>Modifier flags with the same values as the Win32 MOD_* constants.</summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 1,
    Ctrl = 2,
    Shift = 4,
    Win = 8,
}

public sealed record HotkeyChord(HotkeyModifiers Modifiers, uint VirtualKey, string Text)
{
    /// <summary>Parses text like "Ctrl+Shift+F1". Keys: F1–F24, A–Z, 0–9.</summary>
    public static HotkeyChord Parse(string text)
    {
        var modifiers = HotkeyModifiers.None;
        uint? key = null;

        foreach (var raw in text.Split('+'))
        {
            var part = raw.Trim();
            var modifier = part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => HotkeyModifiers.Ctrl,
                "shift" => HotkeyModifiers.Shift,
                "alt" => HotkeyModifiers.Alt,
                "win" => HotkeyModifiers.Win,
                _ => HotkeyModifiers.None,
            };

            if (modifier != HotkeyModifiers.None)
            {
                modifiers |= modifier;
                continue;
            }

            var vk = KeyCode(part);
            if (vk == null || key != null)
                throw Bad(text);
            key = vk;
        }

        return key == null ? throw Bad(text) : new HotkeyChord(modifiers, key.Value, text);
    }

    private static uint? KeyCode(string part)
    {
        var upper = part.ToUpperInvariant();
        if (upper.Length == 1 && upper[0] is (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
            return upper[0];
        if (upper.Length > 1 && upper[0] == 'F' && int.TryParse(upper[1..], out var n) && n is >= 1 and <= 24)
            return (uint)(0x70 + n - 1);
        return null;
    }

    private static FormatException Bad(string text) =>
        new($"\"{text}\" isn't a hotkey this app understands. Use modifiers (Ctrl, Shift, Alt, Win) plus one key: F1–F24, A–Z, or 0–9, e.g. \"Ctrl+Shift+F1\".");
}
