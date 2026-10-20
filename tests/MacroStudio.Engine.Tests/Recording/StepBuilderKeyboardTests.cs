using MacroStudio.Core.Input;
using MacroStudio.Core.Macros.Steps;
using MacroStudio.Core.Recording;
using static MacroStudio.Engine.Tests.Recording.RecordingScript;

namespace MacroStudio.Engine.Tests.Recording;

public sealed class StepBuilderKeyboardTests
{
    [Fact]
    public void TypedLetters_BecomeOneTextStep_IncludingTurkishLetters()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript().Type(Sap, "Çağrı İş öğü").Finish();

        Assert.Equal("Çağrı İş öğü", Assert.IsType<TypeTextStep>(Assert.Single(steps)).Text);
    }

    [Fact]
    public void Backspace_WhileTyping_RemovesTheLastCharacter()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript()
            .Type(Sap, "abc").Key(Sap, VirtualKeys.Back).Type(Sap, "d")
            .Finish();

        Assert.Equal("abd", Assert.IsType<TypeTextStep>(Assert.Single(steps)).Text);
    }

    [Fact]
    public void Backspace_WithNothingTyped_IsRecordedAsAKey()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript().Key(Sap, VirtualKeys.Back).Finish();

        Assert.Equal(VirtualKeys.Back, Assert.IsType<KeyPressStep>(Assert.Single(steps)).VirtualKey);
    }

    [Fact]
    public void ALongPauseWhileTyping_StartsANewTextStep()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript().Type(Sap, "ab").Wait(3000).Type(Sap, "cd").Finish();

        Assert.Equal(["ab", "cd"], steps.Cast<TypeTextStep>().Select(step => step.Text));
    }

    [Fact]
    public void WithoutMergeTyping_EveryCharacterIsItsOwnStep()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript(new RecordingOptions { MergeTyping = false }).Type(Sap, "ab").Finish();

        Assert.Equal(["a", "b"], steps.Cast<TypeTextStep>().Select(step => step.Text));
    }

    [Fact]
    public void AltGrCharacter_IsTypedText()
    {
        // AltGr+Q types @ on a Turkish Q keyboard; Windows reports AltGr as Ctrl+Alt.
        IReadOnlyList<MacroStep> steps = new RecordingScript().Chord(Sap, KeyModifiers.Control | KeyModifiers.Alt, 0x51, "@").Finish();

        Assert.Equal("@", Assert.IsType<TypeTextStep>(Assert.Single(steps)).Text);
    }

    [Fact]
    public void CtrlC_And_CtrlV_BecomeCopyAndPasteSteps()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript()
            .Chord(Excel, KeyModifiers.Control, VirtualKeys.C)
            .Chord(Sap, KeyModifiers.Control, VirtualKeys.V)
            .Finish();

        Assert.IsType<CopyStep>(steps[0]);
        PasteStep paste = Assert.IsType<PasteStep>(steps[1]);
        Assert.True(paste.CleanText);
        Assert.Equal(2, steps.Count);
    }

    [Fact]
    public void WithoutConvertCopyPaste_CtrlC_IsAShortcut()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript(new RecordingOptions { ConvertCopyPaste = false })
            .Chord(Excel, KeyModifiers.Control, VirtualKeys.C)
            .Finish();

        HotkeyStep hotkey = Assert.IsType<HotkeyStep>(Assert.Single(steps));
        Assert.Equal(KeyModifiers.Control, hotkey.Modifiers);
    }

    [Fact]
    public void ShiftArrow_IsAShortcut()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript().Chord(Excel, KeyModifiers.Shift, VirtualKeys.Down).Finish();

        Assert.Equal(KeyModifiers.Shift, Assert.IsType<HotkeyStep>(Assert.Single(steps)).Modifiers);
    }

    [Fact]
    public void TheSameKeyPressedAgain_CountsUpInOneStep()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript()
            .Key(Excel, VirtualKeys.Down).Key(Excel, VirtualKeys.Down).Key(Excel, VirtualKeys.Down)
            .Finish();

        Assert.Equal(3, Assert.IsType<KeyPressStep>(Assert.Single(steps)).PressCount);
    }

    [Fact]
    public void TheSameKeyAfterAPause_IsANewStep()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript().Key(Excel, VirtualKeys.Down).Wait(2000).Key(Excel, VirtualKeys.Down).Finish();

        Assert.Equal(2, steps.Count);
    }

    [Fact]
    public void AltPressedAlone_IsRecorded_CtrlAlone_IsNot()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript()
            .Key(Excel, VirtualKeys.LeftControl)
            .Key(Excel, VirtualKeys.LeftMenu)
            .Finish();

        Assert.Equal(VirtualKeys.LeftMenu, Assert.IsType<KeyPressStep>(Assert.Single(steps)).VirtualKey);
    }

    [Fact]
    public void MacroStudioShortcut_IsNotRecorded()
    {
        var options = new RecordingOptions
        {
            AppShortcuts = new HashSet<KeyShortcut> { new(KeyModifiers.Control | KeyModifiers.Alt, 0x52) },
        };

        IReadOnlyList<MacroStep> steps = new RecordingScript(options).Chord(Excel, KeyModifiers.Control | KeyModifiers.Alt, 0x52).Finish();

        Assert.Empty(steps);
    }

    [Fact]
    public void TypingInAPasswordField_RecordsOnlyOnePasswordStep()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript()
            .Type(Sap, "Gizli123", isPasswordField: true)
            .Key(Sap, VirtualKeys.Back, isPasswordField: true)
            .Key(Sap, VirtualKeys.Return)
            .Finish();

        Assert.True(Assert.IsType<WaitStep>(steps[0]).IsPasswordEntry);
        Assert.Equal(VirtualKeys.Return, Assert.IsType<KeyPressStep>(steps[1]).VirtualKey);
        Assert.Equal(2, steps.Count);
        Assert.DoesNotContain(steps, step => step is TypeTextStep);
    }

    [Fact]
    public void KeysPressedInMacroStudio_AreNotRecorded()
    {
        IReadOnlyList<MacroStep> steps = new RecordingScript().Type(Bar, "abc").Key(Bar, VirtualKeys.Return).Finish();

        Assert.Empty(steps);
    }
}
