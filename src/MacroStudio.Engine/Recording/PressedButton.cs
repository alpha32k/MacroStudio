using MacroStudio.Core.Geometry;
using MacroStudio.Core.Recording;

namespace MacroStudio.Engine.Recording;

/// <summary>A mouse button that is down; becomes a click or a drag when it is released.</summary>
internal sealed class PressedButton(CapturedMouse down, WindowInfo window)
{
    public CapturedMouse Down { get; } = down;

    public WindowInfo Window { get; } = window;

    public List<ScreenPoint> Path { get; } = [down.Position];
}
