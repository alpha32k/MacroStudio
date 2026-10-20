using MacroStudio.Core.Geometry;

namespace MacroStudio.Core.Macros;

/// <summary>
/// The window a step was recorded in. Playback finds it again by program, title and class,
/// and moves mouse positions along when the window has moved since the recording.
/// </summary>
/// <param name="ProcessName">Program name without ".exe", for example "EXCEL".</param>
/// <param name="Bounds">Window position and size at recording time, in physical pixels.</param>
public sealed record WindowTarget(string ProcessName, string Title, string ClassName, ScreenRect Bounds);
