using MacroStudio.Core.Geometry;
using MacroStudio.Core.Input;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Macros.Steps;

namespace MacroStudio.Core.Tests.Macros;

public sealed class ExcelWheelAdviceTests
{
    private static readonly WindowTarget Excel = new("EXCEL", "Kitap1 - Excel", "XLMAIN", new ScreenRect(0, 0, 1200, 800));
    private static readonly WindowTarget Browser = new("chrome", "Intranet", "Chrome_WidgetWin_1", new ScreenRect(0, 0, 1200, 800));

    [Fact]
    public void CountWheelSteps_CountsOnlyExcel()
    {
        MacroStep[] steps = [Wheel(Excel, -120), Wheel(Browser, -120), new CopyStep { Target = Excel }];

        Assert.Equal(1, ExcelWheelAdvice.CountWheelSteps(steps));
    }

    [Theory]
    [InlineData(-120, VirtualKeys.Down, 3)]
    [InlineData(-360, VirtualKeys.Down, 9)]
    [InlineData(240, VirtualKeys.Up, 6)]
    public void ReplaceWheelWithArrowKeys_PressesTheArrowOncePerScrolledRow(int delta, int expectedKey, int expectedPresses)
    {
        MacroStep wheel = Wheel(Excel, delta) with { DelayBeforeMs = 250 };

        KeyPressStep key = Assert.IsType<KeyPressStep>(Assert.Single(ExcelWheelAdvice.ReplaceWheelWithArrowKeys([wheel], linesPerNotch: 3)));

        Assert.Equal(expectedKey, key.VirtualKey);
        Assert.Equal(expectedPresses, key.PressCount);
        Assert.Equal(250, key.DelayBeforeMs);
        Assert.Equal(wheel.Id, key.Id);
    }

    [Fact]
    public void ReplaceWheelWithArrowKeys_LeavesOtherProgramsAlone()
    {
        MacroStep browserWheel = Wheel(Browser, -120);

        Assert.Same(browserWheel, Assert.Single(ExcelWheelAdvice.ReplaceWheelWithArrowKeys([browserWheel], 3)));
    }

    private static MouseWheelStep Wheel(WindowTarget target, int delta) =>
        new() { Delta = delta, Target = target, ScreenPosition = new ScreenPoint(500, 400) };
}
