namespace MacroStudio.Core.Recording;

/// <summary>A raw mouse, keyboard or window event caught during recording, before it becomes a macro step.</summary>
/// <param name="TimeMs">Milliseconds since the recording started, as stamped by Windows.</param>
public abstract record CapturedInput(long TimeMs);
