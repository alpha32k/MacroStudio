using MacroStudio.Core.Geometry;
using MacroStudio.Core.Input;

namespace MacroStudio.Core.Macros.Steps;

/// <summary>Press a button, move along a path, release; for example selecting cells or moving a file.</summary>
public sealed record MouseDragStep : MacroStep
{
    public required MouseButton Button { get; set; }

    public KeyModifiers Modifiers { get; set; }

    public WindowTarget? Target { get; set; }

    /// <summary>Screen positions from press to release, simplified to the points that change the direction.</summary>
    public required IReadOnlyList<ScreenPoint> ScreenPath { get; set; }

    public int DurationMs { get; set; }
}
