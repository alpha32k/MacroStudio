using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroStudio.App.Localization;
using MacroStudio.App.Pages;
using MacroStudio.App.Playback;
using MacroStudio.App.Recording;
using MacroStudio.App.Services;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Playback;

namespace MacroStudio.App.NotchBar;

/// <summary>State and commands of the notch bar. The tray icon menu uses the same commands.</summary>
public sealed partial class NotchBarViewModel : ObservableObject
{
    private static readonly TimeSpan SnoozeDuration = TimeSpan.FromHours(1);

    private readonly MainWindowService _mainWindow;
    private readonly SettingsService _settings;
    private readonly RecordingController _recording;
    private readonly PlaybackController _playback;
    private readonly EditorViewModel _editor;
    private readonly DispatcherTimer _snoozeTimer;
    private string? _selectedPath;

    public NotchBarViewModel(
        MainWindowService mainWindow,
        SettingsService settings,
        RecordingController recording,
        PlaybackController playback,
        MacroLibraryService library,
        EditorViewModel editor)
    {
        _mainWindow = mainWindow;
        _settings = settings;
        _recording = recording;
        _playback = playback;
        _editor = editor;
        Macros = library.Macros;

        _settings.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(IsBarAvailable));
            OnPropertyChanged(nameof(IsPinned));
        };
        _recording.PropertyChanged += (_, _) => OnActivityChanged();
        _playback.PropertyChanged += (_, _) => OnActivityChanged();
        _recording.MacroSaved += (_, macro) => SelectedMacro = macro;
        Macros.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasMacros));

            // Saving reloads the list; the chosen macro is chosen again by its file.
            SelectedMacro ??= Macros.FirstOrDefault(m => string.Equals(m.Path, _selectedPath, StringComparison.OrdinalIgnoreCase));
        };
        Localizer.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(StatusText));

        _snoozeTimer = new DispatcherTimer { Interval = SnoozeDuration };
        _snoozeTimer.Tick += (_, _) => ShowBar();

        SelectedLoop = LoopChoices[0];
        SelectedMacro = Macros.FirstOrDefault();
    }

    /// <summary>Saved macros, newest first.</summary>
    public ObservableCollection<MacroFile> Macros { get; }

    public bool HasMacros => Macros.Count > 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
    public partial MacroFile? SelectedMacro { get; set; }

    public IReadOnlyList<LocalizedOption<LoopSettings>> LoopChoices => PlaybackChoices.Loops;

    [ObservableProperty]
    public partial LocalizedOption<LoopSettings> SelectedLoop { get; set; }

    public IReadOnlyList<double> SpeedChoices => PlaybackChoices.Speeds;

    [ObservableProperty]
    public partial double SelectedSpeed { get; set; } = 1;

    /// <summary>True while the user hid the bar for an hour from the menu.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBarAvailable))]
    public partial bool IsSnoozed { get; set; }

    /// <summary>The bar is switched on in settings and not snoozed.</summary>
    public bool IsBarAvailable => _settings.Current.ShowNotchBar && !IsSnoozed;

    /// <summary>Kept in settings, so a pinned bar is still pinned after a restart.</summary>
    public bool IsPinned => _settings.Current.NotchBarPinned;

    /// <summary>The recording hooks are on; the closed bar shows a pulsing red line.</summary>
    public bool IsRecording => _recording.State == RecordingState.Recording;

    /// <summary>The macro is sending input: the bar must not open, its own pointer moves would open it.</summary>
    public bool IsPlaybackRunning => _playback.State is PlaybackState.CountingDown or PlaybackState.Playing;

    public bool IsPlaybackPaused => _playback.State == PlaybackState.Paused;

    /// <summary>A macro plays or is paused: the bar shows its progress instead of the repeat and speed choices.</summary>
    public bool IsPlaybackActive => _playback.State != PlaybackState.Idle;

    /// <summary>Recording or playing: the bar starts closed even when pinned.</summary>
    public bool IsBusy => _recording.State != RecordingState.Idle || IsPlaybackActive;

    public bool CanChooseMacro => !IsBusy;

    /// <summary>"Tur 4/10 · Adım 6/9" while a macro plays, with "Duraklatıldı" in front while paused.</summary>
    public string StatusText
    {
        get
        {
            if (_playback.Progress is not { } progress)
            {
                return "";
            }

            string key = (IsPlaybackPaused, progress.TotalRounds is null) switch
            {
                (true, false) => "Bar_StatusPaused",
                (true, true) => "Bar_StatusPausedOpenEnded",
                (false, false) => "Bar_StatusPlaying",
                (false, true) => "Bar_StatusPlayingOpenEnded",
            };

            return progress.TotalRounds is int total
                ? Localizer.Instance.Format(key, progress.Round, total, progress.StepNumber, progress.StepCount)
                : Localizer.Instance.Format(key, progress.Round, progress.StepNumber, progress.StepCount);
        }
    }

    [RelayCommand]
    private void TogglePin() => _settings.Update(s => s with { NotchBarPinned = !s.NotchBarPinned });

    [RelayCommand(CanExecute = nameof(CanStartRecording))]
    private Task StartRecording() => _recording.StartAsync();

    private bool CanStartRecording() => _recording.State == RecordingState.Idle && !IsPlaybackActive;

    /// <summary>Plays the chosen macro; while it plays, pauses and resumes it (Ctrl+Alt+P).</summary>
    [RelayCommand(CanExecute = nameof(CanPlayPause), AllowConcurrentExecutions = true)]
    private async Task PlayPause()
    {
        if (_playback.State == PlaybackState.Idle)
        {
            await _playback.PlayAsync(SelectedMacro!.Macro, SelectedLoop.Value, SelectedSpeed);
        }
        else
        {
            _playback.TogglePause();
        }
    }

    private bool CanPlayPause() => _playback.State switch
    {
        PlaybackState.Idle => SelectedMacro is not null && _recording.State == RecordingState.Idle,
        PlaybackState.Playing or PlaybackState.Paused => true,
        _ => false,
    };

    [RelayCommand(CanExecute = nameof(CanStop))]
    private Task Stop()
    {
        if (IsPlaybackActive)
        {
            _playback.Stop();
            return Task.CompletedTask;
        }

        return _recording.StopAsync();
    }

    private bool CanStop() =>
        IsPlaybackActive || _recording.State is RecordingState.CountingDown or RecordingState.Recording;

    [RelayCommand]
    private void OpenMainWindow() => _mainWindow.Show();

    [RelayCommand]
    private void OpenSettings() => _mainWindow.ShowSettings();

    [RelayCommand]
    private void HideBarForOneHour()
    {
        IsSnoozed = true;
        _snoozeTimer.Start();
    }

    [RelayCommand]
    private void ShowBar()
    {
        _snoozeTimer.Stop();
        IsSnoozed = false;
    }

    /// <summary>Unsaved changes in the editor would be lost; the user can save them first or stay.</summary>
    [RelayCommand]
    private void Exit()
    {
        if (_editor.ConfirmDiscard())
        {
            Application.Current.Shutdown();
        }
    }

    /// <summary>A macro remembers the repeat and speed it was saved with; choosing it shows them.</summary>
    partial void OnSelectedMacroChanged(MacroFile? value)
    {
        if (value is null)
        {
            return;
        }

        _selectedPath = value.Path;
        SelectedLoop = PlaybackChoices.LoopFor(value.Macro.Loop, SelectedLoop);
        SelectedSpeed = PlaybackChoices.SpeedFor(value.Macro.SpeedMultiplier, SelectedSpeed);
    }

    private void OnActivityChanged()
    {
        foreach (string name in (string[])[
            nameof(IsRecording), nameof(IsPlaybackRunning), nameof(IsPlaybackPaused), nameof(IsPlaybackActive),
            nameof(IsBusy), nameof(CanChooseMacro), nameof(StatusText)])
        {
            OnPropertyChanged(name);
        }

        StartRecordingCommand.NotifyCanExecuteChanged();
        PlayPauseCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }
}
