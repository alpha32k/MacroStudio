using MacroStudio.Core.Input;
using MacroStudio.Core.Macros.Steps;
using MacroStudio.Core.Recording;
using static MacroStudio.Engine.Tests.Recording.RecordingScript;

namespace MacroStudio.Engine.Tests.Recording;

public sealed class StepBuilderWindowTests
{
    [Fact]
    public void AltTab_BecomesBringToFront_WithoutTheKeys()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript().AltTab(Excel, Sap).Finish();

        FocusWindowStep focus = Assert.IsType<FocusWindowStep>(Assert.Single(steps));
        Assert.Equal("saplogon", focus.Target.ProcessName);
    }

    [Fact]
    public void WithoutConvertWindowSwitches_AltTab_IsAShortcut()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript(new RecordingOptions { ConvertWindowSwitches = false }).AltTab(Excel, Sap).Finish();

        HotkeyStep hotkey = Assert.IsType<HotkeyStep>(Assert.Single(steps));
        Assert.Equal((KeyModifiers.Alt, VirtualKeys.Tab), (hotkey.Modifiers, hotkey.VirtualKey));
    }

    [Fact]
    public void TaskbarClick_ThatBringsAWindowUp_BecomesBringToFront()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript().Click(Taskbar, 400, 1060).Wait(150).Foreground(Sap).Finish();

        Assert.Equal("saplogon", Assert.IsType<FocusWindowStep>(Assert.Single(steps)).Target.ProcessName);
    }

    [Fact]
    public void ClickIntoABackgroundWindow_DoesNotAddABringToFrontStep()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript().Click(Sap, 300, 200).Foreground(Sap).Finish();

        Assert.IsType<MouseClickStep>(Assert.Single(steps));
    }

    [Fact]
    public void ADialogOpenedByTheProgram_IsWaitedFor()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript()
            .Key(Sap, VirtualKeys.Return)
            .Wait(400)
            .Foreground(Dialog)
            .Finish();

        Assert.IsType<KeyPressStep>(steps[0]);
        Assert.Equal("Bilgi", Assert.IsType<FocusWindowStep>(steps[1]).Target.Title);
    }

    [Fact]
    public void FirstStepWaitsNothing_LaterStepsWaitTheRecordedTime()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript()
            .Wait(5000).Chord(Excel, KeyModifiers.Control, VirtualKeys.C)
            .Wait(230).Click(Excel, 300, 200)
            .Finish();

        Assert.Equal(0, steps[0].DelayBeforeMs);

        // The copy step is taken when C goes down; releasing C and then Ctrl takes another 30 + 30 + 20 ms.
        Assert.Equal(230 + 30 + 30 + 20, steps[1].DelayBeforeMs);
    }

    /// <summary>The example of the master prompt: copy a cell in Excel, paste it into SAP, copy back, next row.</summary>
    [Fact]
    public void ExampleScenario_ExcelToSapAndBack()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript()
            .Chord(Excel, KeyModifiers.Control, VirtualKeys.C)
            .AltTab(Excel, Sap)
            .Wait(300).Click(Sap, 640, 210)
            .Chord(Sap, KeyModifiers.Control, VirtualKeys.V)
            .Key(Sap, VirtualKeys.Return)
            .Wait(800).Click(Sap, 640, 330)
            .Chord(Sap, KeyModifiers.Control, VirtualKeys.C)
            .AltTab(Sap, Excel)
            .Key(Excel, 0x27)
            .Chord(Excel, KeyModifiers.Control, VirtualKeys.V)
            .Key(Excel, VirtualKeys.Down)
            .Key(Excel, 0x25)
            .Finish();

        Assert.Equal(
            [
                typeof(CopyStep), typeof(FocusWindowStep), typeof(MouseClickStep), typeof(PasteStep), typeof(KeyPressStep),
                typeof(MouseClickStep), typeof(CopyStep), typeof(FocusWindowStep), typeof(KeyPressStep), typeof(PasteStep),
                typeof(KeyPressStep), typeof(KeyPressStep),
            ],
            steps.Select(step => step.GetType()));
        Assert.Equal("EXCEL", Assert.IsType<FocusWindowStep>(steps[7]).Target.ProcessName);
    }
}
