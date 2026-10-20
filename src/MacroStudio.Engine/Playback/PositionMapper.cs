using MacroStudio.Core.Geometry;
using MacroStudio.Core.Macros;

namespace MacroStudio.Engine.Playback;

/// <summary>Moves a recorded mouse position along with its window, so a window that was moved is still clicked right.</summary>
public static class PositionMapper
{
    /// <param name="currentBounds">Where the window is now; null for steps without a window (desktop, taskbar).</param>
    public static ScreenPoint Map(ScreenPoint recorded, WindowTarget? target, ScreenRect? currentBounds)
    {
        if (target is null || currentBounds is not ScreenRect now)
        {
            return recorded;
        }

        return new ScreenPoint(recorded.X - target.Bounds.Left + now.Left, recorded.Y - target.Bounds.Top + now.Top);
    }
}
