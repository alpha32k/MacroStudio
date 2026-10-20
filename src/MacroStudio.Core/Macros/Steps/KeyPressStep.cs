namespace MacroStudio.Core.Macros.Steps;

/// <summary>A key that does not type a character: Enter, Tab, arrows, Delete, F1–F12 and so on.</summary>
public sealed record KeyPressStep : MacroStep
{
    public required int VirtualKey { get; set; }

    public int ScanCode { get; set; }

    public bool IsExtended { get; set; }

    /// <summary>How many times in a row the key was pressed, for example ↓ ×5.</summary>
    public int PressCount { get; set; } = 1;

    /// <summary>The window that had the keyboard focus.</summary>
    public WindowTarget? Target { get; set; }
}
