using MacroStudio.Core.Geometry;
using MacroStudio.Core.Macros;

namespace MacroStudio.Core.Playback;

/// <summary>Finds controls by name with Windows UI Automation, while recording and while playing.</summary>
public interface IUiElements
{
    /// <summary>The control at a screen point, when it is one worth finding again by name (a button, not a whole page).</summary>
    /// <param name="windowBounds">The window clicked into; controls nearly as large as the window are not described.</param>
    UiElement? Describe(ScreenPoint point, ScreenRect windowBounds);

    /// <summary>Where the control is now in this window; of several matches, the one nearest to <paramref name="near"/>.</summary>
    /// <returns>Null when it is not there or the program took longer than <paramref name="timeout"/> to answer.</returns>
    ScreenRect? Find(nint window, UiElement element, ScreenPoint near, TimeSpan timeout);
}
