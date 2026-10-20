using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroStudio.App.Localization;
using MacroStudio.App.Macros;
using MacroStudio.App.Playback;
using MacroStudio.App.Recording;
using MacroStudio.App.Services;
using MacroStudio.App.Shell;
using MacroStudio.Core.Geometry;
using MacroStudio.Core.Input;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Macros.Steps;
using MacroStudio.Core.Playback;
using MacroStudio.Core.Recording;
using MacroStudio.Engine.Editing;
using MacroStudio.Native.Windowing;
using Serilog;

namespace MacroStudio.App.Pages;

/// <summary>
/// Edits one saved macro: its steps (with undo), its name, repeat and speed. "Play from here" and "Try step"
/// play the edited version without saving it, so a change can be tried first.
/// </summary>
public sealed partial class EditorViewModel : ObservableObject
{
    private const int ClickPickSeconds = 3;

    // "Add step > Key" offers these; other keys and shortcuts come from recording.
    private static readonly (int VirtualKey, bool IsExtended)[] InsertableKeys =
    [
        (0x0D, false), (0x09, false), (0x1B, false), (0x20, false), (0x08, false), (0x2E, true),
        (0x28, true), (0x26, true), (0x25, true), (0x27, true), (0x24, true), (0x23, true),
        (0x21, true), (0x22, true), (0x71, false), (0x74, false),
    ];

    private readonly MacroLibraryService _library;
    private readonly PlaybackController _playback;
    private readonly MainWindowService _mainWindow;
    private readonly IWindowSystem _windows;
    private readonly IWindowInspector _inspector;
    private StepListEditor? _editor;
    private MacroFile? _openFile;
    private bool _detailsChanged;
    private bool _isShowingMacro;
    private List<int> _selection = [];

    public EditorViewModel(
        MacroLibraryService library,
        PlaybackController playback,
        MainWindowService mainWindow,
        IWindowSystem windows,
        IWindowInspector inspector)
    {
        _library = library;
        _playback = playback;
        _mainWindow = mainWindow;
        _windows = windows;
        _inspector = inspector;
        Macros = library.Macros;
        SelectedLoop = PlaybackChoices.Loops[0];
        SelectedSpeed = 1;

        library.Refreshed += (_, _) => FollowOpenMacro();
        Localizer.Instance.PropertyChanged += (_, _) =>
        {
            ShowRows(refreshAll: true);
            OnPropertyChanged(nameof(ScreenWarning));
        };
        _playback.PropertyChanged += (_, _) => NotifyCommands();
    }

    /// <summary>Asks the page to select these lines (indexes) after steps were added, moved or removed.</summary>
    public event EventHandler<IReadOnlyList<int>>? SelectionRequested;

    public ObservableCollection<MacroFile> Macros { get; }

    public ObservableCollection<EditorRow> Rows { get; } = [];

    public IReadOnlyList<LocalizedOption<LoopSettings>> LoopChoices => PlaybackChoices.Loops;

    public IReadOnlyList<double> SpeedChoices => PlaybackChoices.Speeds;

    [ObservableProperty]
    public partial MacroFile? SelectedMacro { get; set; }

    [ObservableProperty]
    public partial string MacroName { get; set; } = "";

    [ObservableProperty]
    public partial LocalizedOption<LoopSettings> SelectedLoop { get; set; }

    [ObservableProperty]
    public partial double SelectedSpeed { get; set; }

    public bool IsOpen => _editor is not null;

    public bool HasChanges => (_editor?.HasChanges ?? false) || _detailsChanged;

    [ObservableProperty]
    public partial string MacroDescription { get; set; } = "";

    /// <summary>Set when the macro clicks and was recorded on a screen of another size or scale.</summary>
    public string? ScreenWarning => _editor is null ? null : ScreenWarningText.For(CurrentMacro());

    private bool HasSelection => _editor is not null && _selection.Count > 0;

    /// <summary>Opens a macro from another page (the home page's "Edit" button).</summary>
    public void Open(string path) =>
        SelectedMacro = Macros.FirstOrDefault(m => SamePath(m.Path, path)) ?? SelectedMacro;

    /// <summary>The page reports which lines are selected.</summary>
    public void SetSelection(IEnumerable<int> indexes)
    {
        _selection = indexes.Order().ToList();
        NotifyCommands();
    }

    public void SetEnabled(int index, bool isEnabled) => _editor?.SetEnabled([index], isEnabled);

    public void SetDelay(int index, string text)
    {
        if (DelayParser.Parse(text) is int delay)
        {
            _editor?.SetDelay([index], delay);
        }
        else
        {
            // Not a valid wait: the line shows the old value again.
            Rows[index].Show(Rows[index].Step);
        }
    }

    public void SetNote(int index, string? note) => _editor?.SetNote(index, note);

    /// <summary>Drag and drop in the table.</summary>
    public void Move(int index, int newIndex)
    {
        if (_editor is null)
        {
            return;
        }

        _selection = [Math.Clamp(newIndex, 0, _editor.Steps.Count - 1)];
        _editor.Move(index, newIndex);
    }

    /// <summary>Offers to save unsaved changes. False when the user wants to stay with them.</summary>
    public bool ConfirmDiscard()
    {
        if (!HasChanges)
        {
            return true;
        }

        MessageBoxResult answer = MessageBox.Show(
            Localizer.Instance.Format("Editor_SaveChanges", _openFile!.Macro.Name),
            "Macro Studio",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);
        return answer switch
        {
            MessageBoxResult.Yes => TrySave(),
            MessageBoxResult.No => true,
            _ => false,
        };
    }

    partial void OnSelectedMacroChanged(MacroFile? value)
    {
        if (_isShowingMacro || value is null || SamePath(value.Path, _openFile?.Path))
        {
            return;
        }

        string path = value.Path;
        if (ConfirmDiscard())
        {
            // Saving the old macro reloaded the list; take the fresh entry.
            Load(Macros.FirstOrDefault(m => SamePath(m.Path, path)) ?? value);
        }

        SelectOpenMacro();
    }

    partial void OnMacroNameChanged(string value) => MarkDetailsChanged();

    partial void OnMacroDescriptionChanged(string value) => MarkDetailsChanged();

    partial void OnSelectedLoopChanged(LocalizedOption<LoopSettings> value) => MarkDetailsChanged();

    partial void OnSelectedSpeedChanged(double value) => MarkDetailsChanged();

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save() => TrySave();

    private bool CanSave() => HasChanges && MacroName.Trim().Length is > 0 and <= MacroValidator.MaxNameLength;

    private bool TrySave()
    {
        if (_editor is null || _openFile is null)
        {
            return false;
        }

        string path = _openFile.Path;
        try
        {
            _library.Save(path, CurrentMacro());
        }
        catch (Exception ex) when (ex is InvalidMacroFileException or IOException or UnauthorizedAccessException)
        {
            Log.Error(ex, "Edited macro could not be saved");
            MessageBox.Show(Localizer.Instance.Format("Save_Failed", ex.Message), "Macro Studio", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        _detailsChanged = false;
        _editor.MarkSaved();
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => _editor?.Undo();

    private bool CanUndo() => _editor?.CanUndo ?? false;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => _editor?.Redo();

    private bool CanRedo() => _editor?.CanRedo ?? false;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteSelected()
    {
        List<int> chosen = _selection;

        // The step that followed the deleted ones gets selected.
        int next = Math.Min(chosen[0], _editor!.Steps.Count - chosen.Count - 1);
        _selection = next >= 0 ? [next] : [];
        _editor.Delete(chosen);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DuplicateSelected()
    {
        List<int> chosen = _selection;
        _selection = chosen.Select((index, order) => index + order + 1).ToList();
        _editor!.Duplicate(chosen);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void MoveUp() => Move(_selection[0], _selection[0] - 1);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void MoveDown() => Move(_selection[0], _selection[0] + 1);

    /// <summary>Disabled steps stay in the macro but are skipped when it plays.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ToggleEnabled() => _editor!.SetEnabled(_selection, !_editor.Steps[_selection[0]].IsEnabled);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void SetDelayOfSelected()
    {
        string? text = InputDialog.AskText(
            Localizer.Instance["Editor_SetDelay"],
            Localizer.Instance["Editor_DelayPrompt"],
            _editor!.Steps[_selection[0]].DelayBeforeMs.ToString(CultureInfo.InvariantCulture));
        if (text is null)
        {
            return;
        }

        if (DelayParser.Parse(text) is int delay)
        {
            _editor.SetDelay(_selection, delay);
        }
        else
        {
            MessageBox.Show(Localizer.Instance["Editor_InvalidDelay"], "Macro Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand(CanExecute = nameof(HasExcelWheel))]
    private void ConvertExcelWheel() =>
        _editor!.ReplaceAll(ExcelWheelAdvice.ReplaceWheelWithArrowKeys(_editor.Steps, SystemParameters.WheelScrollLines));

    private bool HasExcelWheel() => _editor is not null && ExcelWheelAdvice.CountWheelSteps(_editor.Steps) > 0;

    /// <summary>Brings back the macro as it was before the last save; saving again keeps that.</summary>
    [RelayCommand(CanExecute = nameof(IsOpen))]
    private void RestorePreviousVersion()
    {
        MacroDefinition? previous = _library.LoadPreviousVersion(_openFile!.Path);
        if (previous is null)
        {
            MessageBox.Show(Localizer.Instance["Editor_NoPreviousVersion"], "Macro Studio", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ShowDetails(previous);
        _detailsChanged = true;
        _selection = [];
        _editor!.ReplaceAll(previous.Steps);
    }

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private Task PlayFromHere() => PlayAsync(_selection[0] + 1, null, SelectedLoop.Value);

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private Task TryStep() => PlayAsync(_selection[0] + 1, _selection[0] + 1, LoopSettings.Times(1));

    private bool CanPlay() => HasSelection && _playback.State == PlaybackState.Idle;

    [RelayCommand(CanExecute = nameof(IsOpen))]
    private void InsertWait()
    {
        string? text = InputDialog.AskText(Localizer.Instance["Insert_Wait"], Localizer.Instance["Editor_DelayPrompt"], "500");
        if (text is null)
        {
            return;
        }

        if (DelayParser.Parse(text) is int duration)
        {
            InsertStep(new WaitStep { DurationMs = duration });
        }
        else
        {
            MessageBox.Show(Localizer.Instance["Editor_InvalidDelay"], "Macro Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand(CanExecute = nameof(IsOpen))]
    private void InsertKey()
    {
        List<LocalizedOption<(int VirtualKey, bool IsExtended)>> choices = InsertableKeys
            .Select(key => new LocalizedOption<(int VirtualKey, bool IsExtended)>(key, _ => KeyNames.Of(key.VirtualKey)))
            .ToList();
        (int virtualKey, bool isExtended) = InputDialog.AskChoice(Localizer.Instance["Insert_Key"], Localizer.Instance["Insert_KeyPrompt"], choices);
        if (virtualKey != 0)
        {
            InsertStep(new KeyPressStep { VirtualKey = virtualKey, IsExtended = isExtended, Target = TargetBeforeInsertPoint() });
        }
    }

    [RelayCommand(CanExecute = nameof(IsOpen))]
    private void InsertText()
    {
        string? text = InputDialog.AskText(Localizer.Instance["Insert_Text"], Localizer.Instance["Insert_TextPrompt"]);
        if (!string.IsNullOrEmpty(text))
        {
            InsertStep(new TypeTextStep { Text = text, Target = TargetBeforeInsertPoint() });
        }
    }

    [RelayCommand(CanExecute = nameof(IsOpen))]
    private void InsertCopy() => InsertStep(new CopyStep { Target = TargetBeforeInsertPoint() });

    [RelayCommand(CanExecute = nameof(IsOpen))]
    private void InsertPaste() => InsertStep(new PasteStep { Target = TargetBeforeInsertPoint() });

    [RelayCommand(CanExecute = nameof(IsOpen))]
    private void InsertFocusWindow()
    {
        List<LocalizedOption<WindowCandidate?>> choices = _windows.ListWindows()
            .Where(w => w.Title.Length > 0)
            .Select(w => new LocalizedOption<WindowCandidate?>(w, l => l.Format("Window_Name", w.Title, w.ProcessName)))
            .ToList();
        if (choices.Count == 0)
        {
            return;
        }

        WindowCandidate? chosen = InputDialog.AskChoice(Localizer.Instance["Insert_FocusWindow"], Localizer.Instance["Insert_FocusWindowPrompt"], choices);
        if (chosen is not null)
        {
            InsertStep(new FocusWindowStep { Target = new WindowTarget(chosen.ProcessName, chosen.Title, chosen.ClassName, chosen.Bounds) });
        }
    }

    /// <summary>Counts down while the user points at the spot, then adds a left click there.</summary>
    [RelayCommand(CanExecute = nameof(IsOpen))]
    private async Task InsertClick()
    {
        _mainWindow.Minimize();
        try
        {
            await CountdownWindow.RunAsync(ClickPickSeconds, "Insert_ClickTitle", "Insert_ClickHint", CancellationToken.None);
        }
        finally
        {
            _mainWindow.Show();
        }

        if (!MouseCursor.TryGetPosition(out ScreenPoint position))
        {
            return;
        }

        // Desktop and taskbar clicks are played at the screen position, like recorded ones.
        WindowInfo window = _inspector.Describe(WindowInterop.TopLevelWindowAt(position));
        WindowTarget? target = window.Role == WindowRole.Application ? window.Target : null;
        InsertStep(new MouseClickStep { Button = MouseButton.Left, Target = target, ScreenPosition = position });
    }

    private static bool SamePath(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private void Load(MacroFile file)
    {
        if (_editor is not null)
        {
            _editor.Changed -= OnStepsChanged;
        }

        _openFile = file;
        _editor = new StepListEditor(file.Macro.Steps);
        _editor.Changed += OnStepsChanged;
        _selection = [];
        ShowDetails(file.Macro);
        _detailsChanged = false;
        ShowRows(refreshAll: true);
        OnPropertyChanged(nameof(IsOpen));
        OnPropertyChanged(nameof(ScreenWarning));
        NotifyCommands();
    }

    /// <summary>
    /// After every change to the folder: the open macro's file may have been rewritten (saved), renamed in My macros
    /// (same id, new file) or moved to the trash.
    /// </summary>
    private void FollowOpenMacro()
    {
        if (_openFile is null)
        {
            return;
        }

        MacroFile? current = Macros.FirstOrDefault(m => SamePath(m.Path, _openFile.Path))
            ?? Macros.FirstOrDefault(m => m.Macro.Id == _openFile.Macro.Id);
        if (current is null)
        {
            Close();
            return;
        }

        _openFile = current;
        if (!HasChanges)
        {
            ShowDetails(current.Macro);
        }

        SelectOpenMacro();
    }

    private void Close()
    {
        if (_editor is not null)
        {
            _editor.Changed -= OnStepsChanged;
        }

        _editor = null;
        _openFile = null;
        _selection = [];
        _detailsChanged = false;
        ShowRows(refreshAll: true);
        OnPropertyChanged(nameof(IsOpen));
        OnPropertyChanged(nameof(ScreenWarning));
        NotifyCommands();
        SelectOpenMacro();
    }

    // Later, because the combo box may be changing its selection right now.
    private void SelectOpenMacro() => Application.Current.Dispatcher.BeginInvoke(() =>
    {
        _isShowingMacro = true;
        SelectedMacro = Macros.FirstOrDefault(m => SamePath(m.Path, _openFile?.Path));
        _isShowingMacro = false;
    });

    private void ShowDetails(MacroDefinition macro)
    {
        _isShowingMacro = true;
        MacroName = macro.Name;
        MacroDescription = macro.Description;
        SelectedLoop = PlaybackChoices.LoopFor(macro.Loop, PlaybackChoices.Loops[0]);
        SelectedSpeed = PlaybackChoices.SpeedFor(macro.SpeedMultiplier, 1);
        _isShowingMacro = false;
    }

    private MacroDefinition CurrentMacro() => _openFile!.Macro with
    {
        Name = MacroName.Trim(),
        Description = MacroDescription.Trim(),
        Loop = SelectedLoop.Value,
        SpeedMultiplier = SelectedSpeed,
        Steps = _editor!.Steps,
    };

    private void MarkDetailsChanged()
    {
        if (_isShowingMacro || _editor is null)
        {
            return;
        }

        _detailsChanged = true;
        NotifyCommands();
    }

    private void OnStepsChanged(object? sender, EventArgs e)
    {
        ShowRows(refreshAll: false);
        OnPropertyChanged(nameof(ScreenWarning));
        NotifyCommands();
    }

    private void ShowRows(bool refreshAll)
    {
        IReadOnlyList<MacroStep> steps = _editor?.Steps ?? [];
        for (int i = 0; i < steps.Count; i++)
        {
            if (i == Rows.Count)
            {
                Rows.Add(new EditorRow(i, steps[i], this));
            }
            else if (refreshAll || !ReferenceEquals(Rows[i].Step, steps[i]))
            {
                Rows[i].Show(steps[i]);
            }
        }

        while (Rows.Count > steps.Count)
        {
            Rows.RemoveAt(Rows.Count - 1);
        }

        _selection = _selection.Where(index => index < Rows.Count).ToList();
        SelectionRequested?.Invoke(this, _selection);
    }

    /// <summary>New steps go after the selection, or at the end.</summary>
    private void InsertStep(MacroStep step)
    {
        int index = _selection.Count > 0 ? _selection[^1] + 1 : _editor!.Steps.Count;
        _selection = [index];
        _editor!.Insert(index, step);
    }

    /// <summary>A new step acts on the same window as the step before it.</summary>
    private WindowTarget? TargetBeforeInsertPoint()
    {
        int last = _selection.Count > 0 ? _selection[^1] : _editor!.Steps.Count - 1;
        for (int i = last; i >= 0; i--)
        {
            WindowTarget? target = _editor!.Steps[i] switch
            {
                MouseClickStep s => s.Target,
                MouseDragStep s => s.Target,
                MouseWheelStep s => s.Target,
                KeyPressStep s => s.Target,
                HotkeyStep s => s.Target,
                TypeTextStep s => s.Target,
                FocusWindowStep s => s.Target,
                CopyStep s => s.Target,
                PasteStep s => s.Target,
                _ => null,
            };
            if (target is not null)
            {
                return target;
            }
        }

        return null;
    }

    /// <summary>Plays the edited version; the window steps aside while the macro runs.</summary>
    private async Task PlayAsync(int firstStep, int? lastStep, LoopSettings loop)
    {
        _mainWindow.Minimize();
        try
        {
            await _playback.PlayAsync(CurrentMacro(), loop, SelectedSpeed, firstStep, lastStep);
        }
        finally
        {
            _mainWindow.Show();
        }
    }

    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(HasChanges));
        foreach (IRelayCommand command in (IRelayCommand[])[
            SaveCommand, UndoCommand, RedoCommand, DeleteSelectedCommand, DuplicateSelectedCommand, MoveUpCommand,
            MoveDownCommand, ToggleEnabledCommand, SetDelayOfSelectedCommand, ConvertExcelWheelCommand,
            RestorePreviousVersionCommand, PlayFromHereCommand, TryStepCommand, InsertWaitCommand, InsertKeyCommand,
            InsertTextCommand, InsertCopyCommand, InsertPasteCommand, InsertFocusWindowCommand, InsertClickCommand])
        {
            command.NotifyCanExecuteChanged();
        }
    }
}
