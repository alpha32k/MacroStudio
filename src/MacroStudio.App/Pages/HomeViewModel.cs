using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroStudio.App.Localization;
using MacroStudio.App.Macros;
using MacroStudio.App.Playback;
using MacroStudio.App.Recording;
using MacroStudio.App.Services;

namespace MacroStudio.App.Pages;

public sealed record RecentMacro(string Path, string Name, string Summary, string? LastRun);

public sealed partial class HomeViewModel : ObservableObject
{
    private const int RecentCount = 5;

    private readonly MacroLibraryService _library;
    private readonly RunHistoryService _runs;
    private readonly RecordingController _recording;
    private readonly PlaybackController _playback;
    private readonly EditorViewModel _editor;
    private readonly MainWindowService _mainWindow;

    public HomeViewModel(
        MacroLibraryService library,
        RunHistoryService runs,
        RecordingController recording,
        PlaybackController playback,
        EditorViewModel editor,
        MainWindowService mainWindow)
    {
        _library = library;
        _runs = runs;
        _recording = recording;
        _playback = playback;
        _editor = editor;
        _mainWindow = mainWindow;
        library.Refreshed += (_, _) => ShowRecentMacros();
        runs.Changed += (_, _) => ShowRecentMacros();
        Localizer.Instance.PropertyChanged += (_, _) => ShowRecentMacros();
        recording.PropertyChanged += (_, _) => NewRecordingCommand.NotifyCanExecuteChanged();
        playback.PropertyChanged += (_, _) => NewRecordingCommand.NotifyCanExecuteChanged();
        ShowRecentMacros();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMacros))]
    public partial IReadOnlyList<RecentMacro> RecentMacros { get; private set; } = [];

    public bool HasMacros => RecentMacros.Count > 0;

    /// <summary>The window steps aside so the user can go to the program the recording starts in.</summary>
    [RelayCommand(CanExecute = nameof(CanStartRecording))]
    private Task NewRecording()
    {
        _mainWindow.Minimize();
        return _recording.StartAsync();
    }

    private bool CanStartRecording() => _recording.State == RecordingState.Idle && _playback.State == PlaybackState.Idle;

    [RelayCommand]
    private void OpenMacroFolder()
    {
        Directory.CreateDirectory(_library.Folder);
        Process.Start(new ProcessStartInfo(_library.Folder) { UseShellExecute = true });
    }

    [RelayCommand]
    private void ShowAllMacros() => _mainWindow.ShowPage(typeof(MacrosPage));

    [RelayCommand]
    private void Edit(RecentMacro macro)
    {
        _editor.Open(macro.Path);
        _mainWindow.ShowPage(typeof(EditorPage));
    }

    private void ShowRecentMacros()
    {
        RecentMacros = _library.Macros
            .Take(RecentCount)
            .Select(file => new RecentMacro(file.Path, file.Macro.Name, MacroSummary.Of(file), MacroSummary.LastRunOf(_runs.Of(file.Macro.Id))))
            .ToList();
    }
}
