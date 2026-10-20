using MacroStudio.Core.Geometry;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Playback;
using MacroStudio.Engine.Playback;

namespace MacroStudio.Engine.Tests.Playback;

public sealed class WindowMatcherTests
{
    private static readonly ScreenRect Bounds = new(0, 0, 800, 600);

    [Fact]
    public void ATitleChangedByTheProgram_IsStillFound()
    {
        var target = new WindowTarget("notepad", "Hedef.txt - Not Defteri", "Notepad", Bounds);
        WindowCandidate[] windows = [Window(1, "notepad", "Notepad", "*Hedef.txt - Not Defteri")];

        Assert.Equal(1, WindowMatcher.FindBest(target, windows)?.Handle);
    }

    [Fact]
    public void SeveralWindowsOfTheProgram_TheClosestTitleWins()
    {
        var target = new WindowTarget("EXCEL", "Liste.xlsx - Excel", "XLMAIN", Bounds);
        WindowCandidate[] windows =
        [
            Window(1, "EXCEL", "XLMAIN", "Kitap1 - Excel"),
            Window(2, "EXCEL", "XLMAIN", "Liste.xlsx - Excel"),
        ];

        Assert.Equal(2, WindowMatcher.FindBest(target, windows)?.Handle);
    }

    [Fact]
    public void AnotherProgramWithTheSameTitle_IsNotUsed()
    {
        var target = new WindowTarget("EXCEL", "Rapor", "XLMAIN", Bounds);
        WindowCandidate[] windows = [Window(1, "chrome", "Chrome_WidgetWin_1", "Rapor")];

        Assert.Null(WindowMatcher.FindBest(target, windows));
    }

    [Fact]
    public void ADifferentDialogOfTheSameProgram_IsNotUsed()
    {
        var target = new WindowTarget("EXCEL", "Farklı Kaydet", "#32770", Bounds);
        WindowCandidate[] windows = [Window(1, "EXCEL", "#32770", "Microsoft Excel")];

        Assert.Null(WindowMatcher.FindBest(target, windows));
    }

    [Fact]
    public void ProgramNames_AreComparedWithoutCase()
    {
        var target = new WindowTarget("EXCEL", "Liste.xlsx - Excel", "XLMAIN", Bounds);
        WindowCandidate[] windows = [Window(1, "excel", "XLMAIN", "Liste.xlsx - Excel")];

        Assert.NotNull(WindowMatcher.FindBest(target, windows));
    }

    private static WindowCandidate Window(nint handle, string process, string className, string title) =>
        new(handle, process, className, title, Bounds);
}
