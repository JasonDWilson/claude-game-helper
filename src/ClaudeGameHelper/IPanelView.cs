namespace ClaudeGameHelper;

public interface IPanelView
{
    bool IsShown { get; }

    /// <summary>True when the panel has keyboard focus.</summary>
    bool IsActive { get; }

    /// <summary>Shows the panel without taking focus from the game.</summary>
    void ShowPassive();

    /// <summary>Shows the panel, takes focus, and puts the cursor in the question box.</summary>
    void ShowForAsk();

    void HidePanel();
}
