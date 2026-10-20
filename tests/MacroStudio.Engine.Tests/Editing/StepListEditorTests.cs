using MacroStudio.Core.Macros.Steps;
using MacroStudio.Engine.Editing;

namespace MacroStudio.Engine.Tests.Editing;

public sealed class StepListEditorTests
{
    private static StepListEditor Editor(params string[] texts) =>
        new(texts.Select(text => (MacroStep)new TypeTextStep { Text = text }).ToList());

    private static string[] Texts(StepListEditor editor) =>
        editor.Steps.Select(step => ((TypeTextStep)step).Text).ToArray();

    [Fact]
    public void Delete_RemovesTheChosenSteps()
    {
        StepListEditor editor = Editor("a", "b", "c");

        editor.Delete([0, 2]);

        Assert.Equal(["b"], Texts(editor));
        Assert.True(editor.HasChanges);
    }

    [Fact]
    public void Duplicate_PutsACopyAfterTheStep_WithItsOwnId()
    {
        StepListEditor editor = Editor("a", "b");

        editor.Duplicate([0]);

        Assert.Equal(["a", "a", "b"], Texts(editor));
        Assert.NotEqual(editor.Steps[0].Id, editor.Steps[1].Id);
    }

    [Theory]
    [InlineData(0, 2, new[] { "b", "c", "a" })]
    [InlineData(2, 0, new[] { "c", "a", "b" })]
    [InlineData(1, 99, new[] { "a", "c", "b" })]
    public void Move_PutsTheStepAtTheNewPlace(int from, int to, string[] expected)
    {
        StepListEditor editor = Editor("a", "b", "c");

        editor.Move(from, to);

        Assert.Equal(expected, Texts(editor));
    }

    [Fact]
    public void SetDelay_ChangesOnlyTheChosenSteps()
    {
        StepListEditor editor = Editor("a", "b", "c");

        editor.SetDelay([0, 1], 150);

        Assert.Equal([150, 150, 0], editor.Steps.Select(step => step.DelayBeforeMs));
    }

    [Fact]
    public void Undo_And_Redo_GoBackAndForth()
    {
        StepListEditor editor = Editor("a", "b");
        editor.Delete([0]);
        editor.SetEnabled([0], false);

        editor.Undo();
        Assert.True(editor.Steps[0] is TypeTextStep { Text: "b", IsEnabled: true });

        editor.Undo();
        Assert.Equal(["a", "b"], Texts(editor));
        Assert.False(editor.CanUndo);

        editor.Redo();
        editor.Redo();
        Assert.True(editor.Steps[0] is TypeTextStep { Text: "b", IsEnabled: false });
        Assert.False(editor.CanRedo);
    }

    [Fact]
    public void ANewEdit_ClearsRedo()
    {
        StepListEditor editor = Editor("a", "b");
        editor.Delete([0]);
        editor.Undo();

        editor.SetNote(0, "not");

        Assert.False(editor.CanRedo);
    }

    [Fact]
    public void AnEditThatChangesNothing_IsNotAnUndoStep()
    {
        StepListEditor editor = Editor("a");

        editor.SetEnabled([0], true);

        Assert.False(editor.CanUndo);
        Assert.False(editor.HasChanges);
    }

    [Fact]
    public void Undo_KeepsAtLeastTheLast100Edits()
    {
        StepListEditor editor = Editor("a");
        for (int i = 1; i <= 150; i++)
        {
            editor.SetDelay([0], i);
        }

        for (int i = 0; i < 150; i++)
        {
            editor.Undo();
        }

        Assert.Equal(0, editor.Steps[0].DelayBeforeMs);
    }

    [Fact]
    public void EditedStepsAreNewObjects_SoUndoBringsBackTheOriginals()
    {
        StepListEditor editor = Editor("a");
        MacroStep original = editor.Steps[0];

        editor.SetNote(0, "değişti");
        editor.Undo();

        Assert.Same(original, editor.Steps[0]);
        Assert.Null(original.Note);
    }
}
