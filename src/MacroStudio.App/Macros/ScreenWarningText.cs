using MacroStudio.App.Localization;
using MacroStudio.Core.Macros;
using MacroStudio.Native.Displays;

namespace MacroStudio.App.Macros;

public static class ScreenWarningText
{
    /// <summary>The warning for a macro recorded on another screen; null when there is nothing to warn about.</summary>
    public static string? For(MacroDefinition macro)
    {
        ScreenInfo current = Monitors.DescribePrimary();
        if (!ScreenCheck.NeedsWarning(macro, current))
        {
            return null;
        }

        ScreenInfo recorded = macro.RecordedOn!;
        return Localizer.Instance.Format(
            "Screen_Warning",
            recorded.Width,
            recorded.Height,
            Math.Round(recorded.Scale * 100),
            current.Width,
            current.Height,
            Math.Round(current.Scale * 100));
    }
}
