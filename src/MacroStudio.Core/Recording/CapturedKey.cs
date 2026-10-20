using MacroStudio.Core.Input;

namespace MacroStudio.Core.Recording;

/// <param name="Text">The character the key types with the current layout, Shift, AltGr and Caps Lock; null if none.</param>
/// <param name="Modifiers">Modifier keys held before this event; a modifier key does not count itself.</param>
/// <param name="Window">The window that has the keyboard focus.</param>
public sealed record CapturedKey(
    long TimeMs,
    int VirtualKey,
    int ScanCode,
    bool IsExtended,
    bool IsDown,
    string? Text,
    KeyModifiers Modifiers,
    CapturedWindow Window) : CapturedInput(TimeMs);
