namespace MacroStudio.Core.Recording;

/// <summary>Another window came to the front: after Alt+Tab, a taskbar click, a click into it, or a new dialog.</summary>
public sealed record CapturedForeground(long TimeMs, CapturedWindow Window) : CapturedInput(TimeMs);
