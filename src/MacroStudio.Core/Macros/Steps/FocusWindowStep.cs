namespace MacroStudio.Core.Macros.Steps;

/// <summary>
/// Brings a window to the front. Recorded instead of Alt+Tab and taskbar clicks: Alt+Tab goes to the most
/// recently used window and taskbar buttons move, but the window itself can always be found by its name.
/// </summary>
public sealed record FocusWindowStep : MacroStep
{
    public required WindowTarget Target { get; set; }
}
