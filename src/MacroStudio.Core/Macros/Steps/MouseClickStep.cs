using MacroStudio.Core.Geometry;
using MacroStudio.Core.Input;

namespace MacroStudio.Core.Macros.Steps;

public sealed record MouseClickStep : MacroStep
{
    public required MouseButton Button { get; set; }

    /// <summary>1 for a click, 2 for a double click.</summary>
    public int ClickCount { get; set; } = 1;

    /// <summary>Keys held during the click, for example Ctrl for adding to a selection.</summary>
    public KeyModifiers Modifiers { get; set; }

    /// <summary>The clicked window; null for the desktop and the taskbar, which are clicked at the screen position.</summary>
    public WindowTarget? Target { get; set; }

    public required ScreenPoint ScreenPosition { get; set; }
}
