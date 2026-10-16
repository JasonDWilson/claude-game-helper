using Microsoft.Win32;

namespace ClaudeGameHelper.Platform;

/// <summary>Starts the helper when the person signs in to Windows (current user only, no admin).</summary>
public sealed class StartupRegistration(string runKeyPath = StartupRegistration.RunKey, string valueName = "ClaudeGameHelper")
{
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(runKeyPath);
            return key?.GetValue(valueName) != null;
        }
    }

    public void Enable(string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(runKeyPath);
        key.SetValue(valueName, $"\"{exePath}\"");
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
