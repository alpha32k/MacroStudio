using MacroStudio.Core.Geometry;
using MacroStudio.Core.Input;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Macros.Steps;

namespace MacroStudio.Core.Tests.Macros;

public sealed class ScreenAndDurationTests
{
    [Theory]
    [InlineData(1920, 1080, 1.0, true)]
    [InlineData(1920, 1080, 1.5, false)]
    [InlineData(2560, 1440, 1.0, false)]
    public void ScreenInfo_Matches_OnlySameSizeAndScale(int width, int height, double scale, bool expected)
    {
        var recorded = new ScreenInfo(1920, 1080, 1.0);

        Assert.Equal(expected, recorded.Matches(new ScreenInfo(width, height, scale)));
    }

    [Fact]
    public void ScreenCheck_WarnsOnlyForMacrosThatPointAtTheScreen()
    {
        var otherScreen = new ScreenInfo(1366, 768, 1.0);
        var click = new MouseClickStep { Button = MouseButton.Left, ScreenPosition = new ScreenPoint(10, 10) };
        MacroDefinition clicking = new() { Name = "Tık", RecordedOn = new ScreenInfo(1920, 1080, 1.0), Steps = [click] };
        MacroDefinition typing = clicking with { Steps = [new CopyStep(), new PasteStep()] };

        Assert.True(ScreenCheck.NeedsWarning(clicking, otherScreen));
        Assert.False(ScreenCheck.NeedsWarning(clicking, clicking.RecordedOn!));
        Assert.False(ScreenCheck.NeedsWarning(typing, otherScreen));
        Assert.False(ScreenCheck.NeedsWarning(clicking with { RecordedOn = null }, otherScreen));
    }

    [Fact]
    public void EstimateRound_AddsWaitsAndDrags_AtTheSavedSpeed()
    {
        var macro = new MacroDefinition
        {
            Name = "Test",
            SpeedMultiplier = 2,
            Steps =
            [
                new CopyStep { DelayBeforeMs = 400 },
                new WaitStep { DelayBeforeMs = 100, DurationMs = 1000 },
                new MouseDragStep { DelayBeforeMs = 0, DurationMs = 500, Button = MouseButton.Left, ScreenPath = [new ScreenPoint(0, 0), new ScreenPoint(5, 5)] },
                new PasteStep { DelayBeforeMs = 9000, IsEnabled = false },
                new WaitStep { DelayBeforeMs = 0, DurationMs = 60000, IsPasswordEntry = true },
            ],
        };

        Assert.Equal(TimeSpan.FromMilliseconds(1000), MacroDuration.EstimateRound(macro));
    }
}
