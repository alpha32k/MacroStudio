namespace MacroStudio.Core.Macros;

/// <summary>
/// The control a click hit, as Windows UI Automation names it. Playback looks for the same control first and
/// clicks it wherever it is now, so a button that moved (another window size, another screen scale) is still hit;
/// when it is not found, the recorded position is clicked as before.
/// </summary>
/// <param name="AutomationId">The program's own id for the control; often empty.</param>
/// <param name="Name">What the control says, for example "Kaydet".</param>
/// <param name="ControlType">UI Automation's kind, for example "ControlType.Button".</param>
/// <param name="OffsetX">Where in the control the click was, from 0 (left edge) to 1 (right edge).</param>
/// <param name="OffsetY">Where in the control the click was, from 0 (top) to 1 (bottom).</param>
public sealed record UiElement(string AutomationId, string Name, string ControlType, string ClassName, double OffsetX, double OffsetY);
