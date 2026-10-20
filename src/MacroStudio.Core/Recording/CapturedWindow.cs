using MacroStudio.Core.Geometry;

namespace MacroStudio.Core.Recording;

/// <summary>The top-level window an event belongs to, as seen at the moment of the event.</summary>
public readonly record struct CapturedWindow(nint Handle, ScreenRect Bounds);
