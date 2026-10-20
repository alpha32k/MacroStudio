using MacroStudio.Core.Input;

namespace MacroStudio.Core.Macros.Steps;

/// <summary>A key pressed together with Ctrl, Alt, Windows or Shift, for example Ctrl+S or Shift+↓.</summary>
public sealed record HotkeyStep : MacroStep
{
    public required KeyModifiers Modifiers { get; set; }

    public required int VirtualKey { get; set; }

    public int ScanCode { get; set; }

    public bool IsExtended { get; set; }

    public int PressCount { get; set; } = 1;

    public WindowTarget? Target { get; set; }
}
