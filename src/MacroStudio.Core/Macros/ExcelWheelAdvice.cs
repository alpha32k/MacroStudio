using MacroStudio.Core.Input;
using MacroStudio.Core.Macros.Steps;

namespace MacroStudio.Core.Macros;

/// <summary>
/// How far one wheel notch scrolls in Excel depends on Windows settings, zoom and row heights, so a looping
/// macro drifts and silently copies the wrong row. The arrow keys move exactly one cell every time.
/// </summary>
public static class ExcelWheelAdvice
{
    private const int WheelNotch = 120;
    private const int DownScanCode = 0x50;
    private const int UpScanCode = 0x48;

    public static bool IsExcel(WindowTarget? target) =>
        string.Equals(target?.ProcessName, "EXCEL", StringComparison.OrdinalIgnoreCase);

    public static int CountWheelSteps(IReadOnlyList<MacroStep> steps) =>
        steps.Count(IsExcelVerticalWheel);

    /// <summary>
    /// Replaces each vertical wheel step in Excel with ↓ or ↑, pressed as many times as the wheel scrolled rows.
    /// </summary>
    /// <param name="linesPerNotch">The Windows setting "lines to scroll at a time" (3 by default).</param>
    public static IReadOnlyList<MacroStep> ReplaceWheelWithArrowKeys(IReadOnlyList<MacroStep> steps, int linesPerNotch) =>
        steps.Select(step => IsExcelVerticalWheel(step) ? ToArrowKey((MouseWheelStep)step, linesPerNotch) : step).ToList();

    private static bool IsExcelVerticalWheel(MacroStep step) =>
        step is MouseWheelStep { IsHorizontal: false } wheel && IsExcel(wheel.Target);

    private static KeyPressStep ToArrowKey(MouseWheelStep wheel, int linesPerNotch)
    {
        int notches = Math.Max(1, (int)Math.Round(Math.Abs(wheel.Delta) / (double)WheelNotch));
        bool down = wheel.Delta < 0;
        return new KeyPressStep
        {
            Id = wheel.Id,
            DelayBeforeMs = wheel.DelayBeforeMs,
            IsEnabled = wheel.IsEnabled,
            Note = wheel.Note,
            VirtualKey = down ? VirtualKeys.Down : VirtualKeys.Up,
            ScanCode = down ? DownScanCode : UpScanCode,
            IsExtended = true,
            PressCount = Math.Clamp(notches * Math.Max(1, linesPerNotch), 1, MacroValidator.MaxPressCount),
            Target = wheel.Target,
        };
    }
}
