using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MacroStudio.App.Localization;
using MacroStudio.App.Macros;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Macros.Steps;
using Wpf.Ui.Controls;

namespace MacroStudio.App.Pages;

/// <summary>
/// One line of the editor's step table. Line N always shows step N; after an edit the line shows the new step
/// instead of being replaced, so a text box in the table keeps its focus. Edits go to the editor, which can undo them.
/// </summary>
public sealed class EditorRow(int index, MacroStep step, EditorViewModel owner) : ObservableObject
{
    public int Index { get; } = index;

    public int Number => Index + 1;

    public MacroStep Step { get; private set; } = step;

    public SymbolRegular Icon => StepDescriber.IconOf(Step);

    public string Description => StepDescriber.Describe(Step);

    public bool IsEnabled
    {
        get => Step.IsEnabled;
        set => owner.SetEnabled(Index, value);
    }

    /// <summary>Accepts "230", "230 ms", "0,5 sn"; anything else brings the old value back.</summary>
    public string DelayText
    {
        get => Step.DelayBeforeMs.ToString(CultureInfo.InvariantCulture);
        set => owner.SetDelay(Index, value);
    }

    public string? Note
    {
        get => Step.Note;
        set => owner.SetNote(Index, value);
    }

    public string? Warning => StepWarnings.Of(Step) is var warning and not StepWarning.None
        ? Localizer.Instance["Warning_" + warning]
        : null;

    public void Show(MacroStep step)
    {
        Step = step;

        // An empty name tells the bindings that every property changed.
        OnPropertyChanged(string.Empty);
    }
}
