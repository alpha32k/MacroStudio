using MacroStudio.Core.Geometry;
using MacroStudio.Core.Input;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Macros.Steps;
using MacroStudio.Core.Recording;
using MacroStudio.Engine.Recording;
using static MacroStudio.Engine.Tests.Recording.RecordingScript;

namespace MacroStudio.Engine.Tests.Recording;

public sealed class StepBuilderMouseTests
{
    [Fact]
    public void Click_RecordsTheWindowAndTheScreenPosition()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript().Click(Excel, 300, 200).Finish();

        MouseClickStep click = Assert.IsType<MouseClickStep>(Assert.Single(steps));
        Assert.Equal(new ScreenPoint(300, 200), click.ScreenPosition);
        Assert.Equal("EXCEL", click.Target?.ProcessName);
        Assert.Equal(1, click.ClickCount);
    }

    [Fact]
    public void Click_KeepsTheControlUnderThePress_ButADragDoesNot()
    {
        var element = new UiElement("btnSave", "Kaydet", "ControlType.Button", "Button", 0.5, 0.5);
        var builder = new StepBuilder(new RecordingOptions());
        builder.AddMouse(new CapturedMouse(0, MouseAction.Down, MouseButton.Left, new ScreenPoint(300, 200), 0, KeyModifiers.None, Sap.Captured), Sap.Info, element);
        builder.AddMouse(new CapturedMouse(60, MouseAction.Up, MouseButton.Left, new ScreenPoint(300, 200), 0, KeyModifiers.None, Sap.Captured), Sap.Info);
        builder.AddMouse(new CapturedMouse(2000, MouseAction.Down, MouseButton.Left, new ScreenPoint(300, 200), 0, KeyModifiers.None, Sap.Captured), Sap.Info, element);
        builder.AddMouse(new CapturedMouse(2300, MouseAction.Up, MouseButton.Left, new ScreenPoint(500, 400), 0, KeyModifiers.None, Sap.Captured), Sap.Info);

        IReadOnlyList<MacroStep> steps = builder.Finish();

        Assert.Equal(element, Assert.IsType<MouseClickStep>(steps[0]).Element);
        Assert.IsType<MouseDragStep>(steps[1]);
    }

    [Fact]
    public void Click_KeepsTheHeldModifier()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript().Click(Excel, 300, 200, modifiers: KeyModifiers.Control).Finish();

        Assert.Equal(KeyModifiers.Control, Assert.IsType<MouseClickStep>(Assert.Single(steps)).Modifiers);
    }

    [Fact]
    public void TwoQuickClicksOnTheSameSpot_BecomeADoubleClick()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript().Click(Excel, 300, 200).Wait(150).Click(Excel, 301, 201).Finish();

        Assert.Equal(2, Assert.IsType<MouseClickStep>(Assert.Single(steps)).ClickCount);
    }

    [Fact]
    public void TwoSlowClicks_StayTwoClicks()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript().Click(Excel, 300, 200).Wait(800).Click(Excel, 300, 200).Finish();

        Assert.Equal(2, steps.Count);
        Assert.All(steps, step => Assert.Equal(1, Assert.IsType<MouseClickStep>(step).ClickCount));
    }

    [Fact]
    public void PressMoveRelease_BecomesADragWithASimplifiedPath()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript().Drag(Excel, new ScreenPoint(100, 100), new ScreenPoint(400, 100)).Finish();

        MouseDragStep drag = Assert.IsType<MouseDragStep>(Assert.Single(steps));
        Assert.Equal([new ScreenPoint(100, 100), new ScreenPoint(400, 100)], drag.ScreenPath);
    }

    [Fact]
    public void ClicksOnMacroStudio_AreNotRecorded()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript().Click(Bar, 960, 10).Click(Excel, 300, 200).Finish();

        Assert.IsType<MouseClickStep>(Assert.Single(steps));
    }

    [Fact]
    public void DesktopClick_HasNoWindowAndUsesTheScreenPosition()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript().Click(Desktop, 50, 60).Finish();

        MouseClickStep click = Assert.IsType<MouseClickStep>(Assert.Single(steps));
        Assert.Null(click.Target);
        Assert.Equal(new ScreenPoint(50, 60), click.ScreenPosition);
    }

    [Fact]
    public void WheelNotchesInARow_BecomeOneStep()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript()
            .Wheel(Excel, -120).Wait(100).Wheel(Excel, -120).Wait(100).Wheel(Excel, -120)
            .Finish();

        Assert.Equal(-360, Assert.IsType<MouseWheelStep>(Assert.Single(steps)).Delta);
    }

    [Fact]
    public void WheelInTheOtherDirection_StartsANewStep()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript().Wheel(Excel, -120).Wait(100).Wheel(Excel, 120).Finish();

        Assert.Equal([-120, 120], steps.Cast<MouseWheelStep>().Select(step => step.Delta));
    }
}
