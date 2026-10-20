using MacroStudio.Core.Input;
using MacroStudio.Core.Macros.Steps;

namespace MacroStudio.Core.Macros;

public static class StepWarnings
{
    public static StepWarning Of(MacroStep step) => step switch
    {
        MouseWheelStep { IsHorizontal: false } wheel when ExcelWheelAdvice.IsExcel(wheel.Target) => StepWarning.UnreliableExcelWheel,
        HotkeyStep { Modifiers: KeyModifiers.Alt, VirtualKey: VirtualKeys.Tab } => StepWarning.UnconvertedAltTab,
        _ => StepWarning.None,
    };
}
