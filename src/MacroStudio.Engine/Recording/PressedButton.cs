using MacroStudio.Core.Geometry;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Recording;

namespace MacroStudio.Engine.Recording;

/// <summary>A mouse button that is down; becomes a click or a drag when it is released.</summary>
internal sealed class PressedButton(CapturedMouse down, WindowInfo window, UiElement? element)
{
    public CapturedMouse Down { get; } = down;

    public WindowInfo Window { get; } = window;

    /// <summary>The control under the press; kept when the press becomes a click.</summary>
    public UiElement? Element { get; } = element;

    public List<ScreenPoint> Path { get; } = [down.Position];
}
