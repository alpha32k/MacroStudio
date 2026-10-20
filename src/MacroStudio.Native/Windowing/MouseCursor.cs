using MacroStudio.Core.Geometry;
using static MacroStudio.Native.Interop.NativeMethods;

namespace MacroStudio.Native.Windowing;

public static class MouseCursor
{
    /// <summary>Fails while a secure desktop (UAC prompt, lock screen) is active.</summary>
    public static bool TryGetPosition(out ScreenPoint position)
    {
        bool ok = GetCursorPos(out POINT point);
        position = new ScreenPoint(point.X, point.Y);
        return ok;
    }
}
