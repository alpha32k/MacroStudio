using MacroStudio.Core.Geometry;
using MacroStudio.Core.Input;

namespace MacroStudio.Core.Recording;

/// <param name="WheelDelta">120 per notch; positive is up (or right). Zero for other actions.</param>
/// <param name="Window">The window under the pointer.</param>
public sealed record CapturedMouse(
    long TimeMs,
    MouseAction Action,
    MouseButton Button,
    ScreenPoint Position,
    int WheelDelta,
    KeyModifiers Modifiers,
    CapturedWindow Window) : CapturedInput(TimeMs);
