using MacroStudio.Core.Geometry;
using MacroStudio.Core.Input;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Macros.Steps;

namespace MacroStudio.Core.Tests.Macros;

public sealed class StepWarningsTests
{
    private static readonly WindowTarget Excel = new("EXCEL", "Liste - Excel", "XLMAIN", new ScreenRect(0, 0, 800, 600));
    private static readonly WindowTarget Word = new("WINWORD", "Belge - Word", "OpusApp", new ScreenRect(0, 0, 800, 600));

    [Fact]
    public void WheelInExcel_IsUnreliable_WheelInWord_IsFine()
    {
        Assert.Equal(StepWarning.UnreliableExcelWheel, StepWarnings.Of(Wheel(Excel)));
        Assert.Equal(StepWarning.None, StepWarnings.Of(Wheel(Word)));
    }

    [Fact]
    public void AltTabKeptAsKeys_IsWarnedAbout()
    {
        var altTab = new HotkeyStep { Modifiers = KeyModifiers.Alt, VirtualKey = VirtualKeys.Tab };

        Assert.Equal(StepWarning.UnconvertedAltTab, StepWarnings.Of(altTab));
    }

    private static MouseWheelStep Wheel(WindowTarget target) =>
        new() { Delta = -120, Target = target, ScreenPosition = new ScreenPoint(10, 10) };
}
