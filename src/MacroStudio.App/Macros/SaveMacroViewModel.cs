using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroStudio.App.Localization;
using MacroStudio.App.Services;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Macros.Steps;
using Serilog;

namespace MacroStudio.App.Macros;

/// <summary>
/// Names a new macro and shows its steps before it goes into the library: after a recording, and when a macro
/// file from someone else is imported (so the user sees what it will type and click before it ever runs).
/// </summary>
public sealed partial class SaveMacroViewModel : ObservableObject
{
    private readonly MacroDefinition _draft;
    private readonly MacroLibraryService _library;
    private IReadOnlyList<MacroStep> _steps;

    /// <param name="draft">Everything but the name; its steps are shown.</param>
    /// <param name="isImport">The macro comes from a file: it keeps its name and gets a new id.</param>
    public SaveMacroViewModel(MacroDefinition draft, MacroLibraryService library, bool isImport)
    {
        _draft = draft;
        _library = library;
        _steps = draft.Steps;
        IsImport = isImport;
        Name = isImport
            ? FreeName(number => number == 1 ? draft.Name : $"{draft.Name} ({number})")
            : FreeName(number => Localizer.Instance.Format("Save_DefaultName", number));
        ShowSteps();
    }

    /// <summary>Raised after a successful save; the window closes itself.</summary>
    public event EventHandler? Saved;

    public bool IsImport { get; }

    public string Title => Localizer.Instance[IsImport ? "Import_Title" : "Save_Title"];

    public string SaveText => Localizer.Instance[IsImport ? "Import_Import" : "Save_Save"];

    public string CancelText => Localizer.Instance[IsImport ? "Dialog_Cancel" : "Save_Discard"];

    public MacroFile? SavedMacro { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<StepRow> Rows { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExcelWheel), nameof(ExcelWheelMessage))]
    public partial int ExcelWheelCount { get; private set; }

    public bool HasExcelWheel => ExcelWheelCount > 0;

    public string ExcelWheelMessage => Localizer.Instance.Format("Save_ExcelWheelMessage", ExcelWheelCount);

    public string StepCountText => Localizer.Instance.Format(IsImport ? "Import_StepCount" : "Save_StepCount", _steps.Count);

    [RelayCommand]
    private void ConvertWheel()
    {
        _steps = ExcelWheelAdvice.ReplaceWheelWithArrowKeys(_steps, SystemParameters.WheelScrollLines);
        ShowSteps();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        MacroDefinition macro = _draft with
        {
            // Two imports of the same file are two macros, each with its own run history.
            Id = IsImport ? Guid.NewGuid() : _draft.Id,
            Name = Name.Trim(),
            Steps = _steps,
        };

        try
        {
            SavedMacro = _library.SaveNew(macro);
        }
        catch (Exception ex) when (ex is InvalidMacroFileException or IOException or UnauthorizedAccessException)
        {
            Log.Error(ex, "New macro could not be saved");
            MessageBox.Show(Localizer.Instance.Format("Save_Failed", ex.Message), "Macro Studio", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Saved?.Invoke(this, EventArgs.Empty);
    }

    private bool CanSave() => Name.Trim().Length is > 0 and <= MacroValidator.MaxNameLength;

    private void ShowSteps()
    {
        Rows = StepDescriber.ToRows(_steps);
        ExcelWheelCount = ExcelWheelAdvice.CountWheelSteps(_steps);
    }

    /// <summary>The first name not taken yet: "Makro 1", "Makro 2" … or "Rapor", "Rapor (2)" ….</summary>
    private string FreeName(Func<int, string> candidate)
    {
        var taken = _library.Macros.Select(m => m.Macro.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (int number = 1; ; number++)
        {
            string name = candidate(number);
            if (!taken.Contains(name))
            {
                return name;
            }
        }
    }
}
