using MacroStudio.Core.Geometry;

namespace MacroStudio.Core.Playback;

public interface IWindowSystem
{
    /// <summary>Visible top-level windows, front to back.</summary>
    IReadOnlyList<WindowCandidate> ListWindows();

    nint ForegroundWindow { get; }

    /// <summary>Brings the window to the front; true when it is in front afterwards.</summary>
    bool TryActivate(nint window);

    ScreenRect? GetBounds(nint window);

    /// <summary>
    /// Puts the window at the given place and size (a maximized window is restored first). False when the place
    /// is not on any connected monitor or Windows refused the move.
    /// </summary>
    bool TryMove(nint window, ScreenRect bounds);

    /// <summary>True when the window's program runs with more rights than Macro Studio, so input to it is blocked.</summary>
    bool IsElevatedAboveUs(nint window);

    /// <summary>Opens a program, file or web address as Explorer would; false when Windows could not open it.</summary>
    bool TryOpen(string path, string arguments);

    /// <summary>The color of one screen pixel as 0xRRGGBB; null when the screen cannot be read (for example locked).</summary>
    int? PixelAt(ScreenPoint point);

    /// <summary>A copy of part of the screen; null when the screen cannot be read.</summary>
    Bgra32Image? Capture(ScreenRect area);

    /// <summary>All monitors together, in physical pixels.</summary>
    ScreenRect DesktopBounds { get; }
}
