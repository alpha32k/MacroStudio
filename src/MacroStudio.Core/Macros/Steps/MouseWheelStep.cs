using MacroStudio.Core.Geometry;
using MacroStudio.Core.Input;

namespace MacroStudio.Core.Macros.Steps;

/// <summary>
/// Turning the mouse wheel. How far one notch scrolls depends on Windows settings, zoom and row heights,
/// so in Excel the arrow keys are the reliable way to move from row to row.
/// </summary>
public sealed record MouseWheelStep : MacroStep
{
    /// <summary>120 is one notch. Positive scrolls up (or right), negative scrolls down (or left).</summary>
    public required int Delta { get; set; }

    public bool IsHorizontal { get; set; }

    /// <summary>Keys held while turning the wheel; Ctrl + wheel zooms in many programs.</summary>
    public KeyModifiers Modifiers { get; set; }

    public WindowTarget? Target { get; set; }

    public required ScreenPoint ScreenPosition { get; set; }
}
