namespace ClaudeGameHelper.Platform;

/// <summary>
/// Undoes environment settings the helper can inherit when it's started from inside a code editor
/// (VS Code, Cursor) instead of from Windows.
/// </summary>
public static class EditorEnvironment
{
    /// <summary>
    /// Editors set ELECTRON_RUN_AS_NODE=1 for programs they launch. Anything the helper opens inherits it, and
    /// an Electron app such as VS Code then runs as a bare script runner and quits without a window, so
    /// "Settings" or "Open tracking file" silently does nothing.
    /// </summary>
    public static void RemoveInheritedVariables() =>
        Environment.SetEnvironmentVariable("ELECTRON_RUN_AS_NODE", null);
}
