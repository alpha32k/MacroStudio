using MacroStudio.Core.Macros.Steps;

namespace MacroStudio.Core.Macros;

public static class ScreenCheck
{
    /// <summary>
    /// True when the macro points at spots on the screen and was recorded on a screen of another size or scale:
    /// those spots may now be elsewhere. Keyboard-only macros are not affected.
    /// </summary>
    public static bool NeedsWarning(MacroDefinition macro, ScreenInfo current) =>
        macro.RecordedOn is { } recorded
        && !recorded.Matches(current)
        && macro.Steps.Any(step => step.IsEnabled && step is MouseClickStep or MouseDragStep or MouseWheelStep);
}
