using MacroStudio.Core.Geometry;

namespace MacroStudio.Core.Playback;

/// <summary>An open top-level window that playback may use for a step.</summary>
public sealed record WindowCandidate(nint Handle, string ProcessName, string ClassName, string Title, ScreenRect Bounds);
